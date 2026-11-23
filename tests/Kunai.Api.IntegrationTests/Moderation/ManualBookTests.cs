using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Identity;
using Kunai.Api.IntegrationTests.Infrastructure;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Kunai.Api.IntegrationTests.Moderation;

public sealed class ManualBookTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Manual_books_are_private_until_a_moderator_approves_them()
    {
        var title = "Kürk Mantolu Madonna " + Guid.NewGuid().ToString("N")[..6];
        var author = await factory.CreateSignedInClientAsync();
        var reader = await factory.CreateSignedInClientAsync();
        var moderator = await factory.CreateModeratorClientAsync();
        StubEmptyBookSearch(title);

        var created = await author.PostAsJsonAsync("/api/v1/items", new { title, authors = new[] { "Sabahattin Ali" }, year = 1943, pageCount = 160 }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = item.GetProperty("id").GetString();
        item.GetProperty("catalogStatus").GetString().Should().Be("pendingReview");
        item.GetProperty("ref").ValueKind.Should().Be(JsonValueKind.Null);

        (await author.PutAsJsonAsync($"/api/v1/me/shelf/{id}", new { status = "completed" }, Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await reader.GetAsync($"/api/v1/items/{id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await reader.PutAsJsonAsync($"/api/v1/me/shelf/{id}", new { status = "planned" }, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Search(reader, title)).Should().BeEmpty();

        var pending = await moderator.GetFromJsonAsync<JsonElement>("/api/v1/admin/items/pending?limit=100", Ct);
        pending.EnumerateArray().Select(p => p.GetProperty("item").GetProperty("id").GetString()).Should().Contain(id);
        (await moderator.PostAsync($"/api/v1/admin/items/{id}/approve", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await moderator.PostAsync($"/api/v1/admin/items/{id}/approve", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await reader.GetAsync($"/api/v1/items/{id}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        var hits = await Search(reader, title);
        hits.Should().ContainSingle(h => h.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task Regular_users_cannot_moderate()
    {
        var client = await factory.CreateSignedInClientAsync();

        (await client.GetAsync("/api/v1/admin/items/pending", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Isbn_is_checked_and_duplicates_are_refused()
    {
        var client = await factory.CreateSignedInClientAsync();
        var isbn = Isbn13("978" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture));

        var badCheckDigit = await client.PostAsJsonAsync("/api/v1/items", new { title = "Bad ISBN", isbn13 = isbn[..12] + ((isbn[12] - '0' + 1) % 10) }, Ct);
        (await SignInTests.ShouldBeProblem(badCheckDigit, HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors").GetProperty("isbn13")[0].GetString().Should().Be("isbn_invalid");

        (await client.PostAsJsonAsync("/api/v1/items", new { title = "First", isbn13 = isbn }, Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        await SignInTests.ShouldBeProblem(await client.PostAsJsonAsync("/api/v1/items", new { title = "Second", isbn13 = isbn }, Ct), HttpStatusCode.Conflict, "book_exists");
    }

    private void StubEmptyBookSearch(string title) =>
        factory.Providers.Given(Request.Create().WithPath("/gbooks/v1/volumes").WithParam("q", title).UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new { totalItems = 0, items = Array.Empty<object>() }));

    private static async Task<List<JsonElement>> Search(HttpClient client, string title)
    {
        var response = await client.GetAsync($"/api/v1/search?q={Uri.EscapeDataString(title)}&type=book", Ct);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return [];
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("ref").ValueKind == JsonValueKind.Null).ToList();
    }

    private static string Isbn13(string first12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return first12 + ((10 - (sum % 10)) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
