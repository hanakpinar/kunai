using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.Features.Catalog;
using Kunai.Api.IntegrationTests.Identity;
using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Library;

public sealed class ShelfTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_creates_then_replaces_an_entry_with_a_changing_etag()
    {
        var client = await factory.CreateSignedInClientAsync();
        var bookId = await factory.CreateItemAsync(ItemType.Book, progressTotal: 412, title: "Dune");

        var created = await client.PutAsJsonAsync($"/api/v1/me/shelf/{bookId}", new { status = "inProgress", startedOn = "2026-11-01", progress = 120 }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location!.ToString().Should().Be($"/api/v1/me/shelf/{bookId}");
        var firstTag = created.Headers.ETag!.Tag;

        var replaced = await client.PutAsJsonAsync($"/api/v1/me/shelf/{bookId}", new { status = "completed", rating = 9, startedOn = "2026-11-01", finishedOn = "2026-11-20", progress = 412, review = "Muhteşem.", isFavorite = true }, Ct);
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        replaced.Headers.ETag!.Tag.Should().NotBe(firstTag);

        var entry = await client.GetFromJsonAsync<JsonElement>($"/api/v1/me/shelf/{bookId}", Ct);
        entry.GetProperty("status").GetString().Should().Be("completed");
        entry.GetProperty("rating").GetInt32().Should().Be(9);
        entry.GetProperty("finishedOn").GetString().Should().Be("2026-11-20");
        entry.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        entry.GetProperty("item").GetProperty("title").GetString().Should().Be("Dune");
    }

    [Fact]
    public async Task If_match_protects_against_lost_updates_and_if_none_match_only_creates()
    {
        var client = await factory.CreateSignedInClientAsync();
        var movieId = await factory.CreateItemAsync(ItemType.Movie);
        var created = await client.PutAsJsonAsync($"/api/v1/me/shelf/{movieId}", new { status = "planned" }, Ct);
        var tag = created.Headers.ETag!.Tag;

        await client.PutAsJsonAsync($"/api/v1/me/shelf/{movieId}", new { status = "completed", rating = 7 }, Ct);

        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/me/shelf/{movieId}") { Content = JsonContent.Create(new { status = "completed", rating = 3 }) };
        stale.Headers.IfMatch.ParseAdd(tag);
        await SignInTests.ShouldBeProblem(await client.SendAsync(stale, Ct), HttpStatusCode.PreconditionFailed, "precondition_failed");

        using var createOnly = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/me/shelf/{movieId}") { Content = JsonContent.Create(new { status = "planned" }) };
        createOnly.Headers.IfNoneMatch.ParseAdd("*");
        (await client.SendAsync(createOnly, Ct)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var current = await client.GetAsync($"/api/v1/me/shelf/{movieId}", Ct);
        using var conditionalGet = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/me/shelf/{movieId}");
        conditionalGet.Headers.IfNoneMatch.Add(current.Headers.ETag!);
        (await client.SendAsync(conditionalGet, Ct)).StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Theory]
    [InlineData(ItemType.Movie, null, """{"status":"inProgress"}""", "status_not_allowed")]
    [InlineData(ItemType.Book, null, """{"status":"planned","rating":8}""", "planned_has_no_activity")]
    [InlineData(ItemType.Book, 300, """{"status":"inProgress","progress":301}""", "progress_exceeds_total")]
    [InlineData(ItemType.Movie, null, """{"status":"completed","progress":1}""", "progress_not_tracked")]
    [InlineData(ItemType.Game, null, """{"status":"inProgress","progress":101}""", "progress_exceeds_total")]
    public async Task Business_rules_answer_422_with_a_field_code(ItemType type, int? total, string body, string code)
    {
        var client = await factory.CreateSignedInClientAsync();
        var itemId = await factory.CreateItemAsync(type, total);

        var response = await client.PutAsync($"/api/v1/me/shelf/{itemId}", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        await SignInTests.ShouldBeProblem(response, HttpStatusCode.UnprocessableEntity, code);
    }

    [Fact]
    public async Task Request_only_rules_answer_400()
    {
        var client = await factory.CreateSignedInClientAsync();
        var bookId = await factory.CreateItemAsync(ItemType.Book);

        var badRating = await client.PutAsJsonAsync($"/api/v1/me/shelf/{bookId}", new { status = "completed", rating = 11 }, Ct);
        var ratingProblem = await SignInTests.ShouldBeProblem(badRating, HttpStatusCode.BadRequest, "validation_failed");
        ratingProblem.GetProperty("errors").GetProperty("rating")[0].GetString().Should().Be("rating_out_of_range");

        // Cross-field rules run once every single field is valid.
        var badDates = await client.PutAsJsonAsync($"/api/v1/me/shelf/{bookId}", new { status = "completed", startedOn = "2026-05-02", finishedOn = "2026-05-01" }, Ct);
        var datesProblem = await SignInTests.ShouldBeProblem(badDates, HttpStatusCode.BadRequest, "validation_failed");
        datesProblem.GetProperty("errors").GetProperty("finishedOn")[0].GetString().Should().Be("finished_before_started");
    }

    [Fact]
    public async Task Delete_is_idempotent_and_unknown_items_are_404()
    {
        var client = await factory.CreateSignedInClientAsync();
        var animeId = await factory.CreateItemAsync(ItemType.Anime);
        await client.PutAsJsonAsync($"/api/v1/me/shelf/{animeId}", new { status = "planned" }, Ct);

        (await client.DeleteAsync($"/api/v1/me/shelf/{animeId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/v1/me/shelf/{animeId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await SignInTests.ShouldBeProblem(await client.GetAsync($"/api/v1/me/shelf/{animeId}", Ct), HttpStatusCode.NotFound, "shelf_entry_not_found");
        await SignInTests.ShouldBeProblem(await client.PutAsJsonAsync($"/api/v1/me/shelf/{Guid.NewGuid()}", new { status = "planned" }, Ct), HttpStatusCode.NotFound, "item_not_found");
    }

    [Fact]
    public async Task Another_user_never_sees_my_entries()
    {
        var alice = await factory.CreateSignedInClientAsync();
        var bob = await factory.CreateSignedInClientAsync();
        var mangaId = await factory.CreateItemAsync(ItemType.Manga);
        await alice.PutAsJsonAsync($"/api/v1/me/shelf/{mangaId}", new { status = "planned" }, Ct);

        (await bob.GetAsync($"/api/v1/me/shelf/{mangaId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await bob.GetFromJsonAsync<JsonElement>("/api/v1/me/shelf", Ct)).GetProperty("totalCount").GetInt32().Should().Be(0);
        (await bob.DeleteAsync($"/api/v1/me/shelf/{mangaId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await alice.GetAsync($"/api/v1/me/shelf/{mangaId}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Shelf_list_filters_and_pages_by_rating_without_repeats()
    {
        var client = await factory.CreateSignedInClientAsync();
        var ratings = new[] { 9, 7, 9, 5, 8 };
        foreach (var rating in ratings)
        {
            var bookId = await factory.CreateItemAsync(ItemType.Book);
            await client.PutAsJsonAsync($"/api/v1/me/shelf/{bookId}", new { status = "completed", rating, finishedOn = "2025-06-01", isFavorite = rating >= 9 }, Ct);
        }

        var movieId = await factory.CreateItemAsync(ItemType.Movie);
        await client.PutAsJsonAsync($"/api/v1/me/shelf/{movieId}", new { status = "planned" }, Ct);

        var seen = new List<int>();
        string? cursor = null;
        do
        {
            var url = "/api/v1/me/shelf?type=book&status=completed&sort=-rating&limit=2" + (cursor is null ? string.Empty : "&cursor=" + cursor);
            var page = await client.GetFromJsonAsync<JsonElement>(url, Ct);
            page.GetProperty("totalCount").GetInt32().Should().Be(5);
            seen.AddRange(page.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("rating").GetInt32()));
            cursor = page.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);

        seen.Should().Equal(9, 9, 8, 7, 5);
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/me/shelf?favorite=true", Ct)).GetProperty("totalCount").GetInt32().Should().Be(2);
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/me/shelf?year=2025", Ct)).GetProperty("totalCount").GetInt32().Should().Be(5);
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/me/shelf?status=planned&status=completed", Ct)).GetProperty("totalCount").GetInt32().Should().Be(6);
        await SignInTests.ShouldBeProblem(await client.GetAsync("/api/v1/me/shelf?sort=-rating&cursor=bogus", Ct), HttpStatusCode.BadRequest, "invalid_cursor");
    }
}
