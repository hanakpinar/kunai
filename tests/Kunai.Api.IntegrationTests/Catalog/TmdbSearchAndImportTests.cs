using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Kunai.Api.IntegrationTests.Catalog;

public sealed class TmdbSearchAndImportTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Movie_search_returns_refs_years_covers_and_a_next_cursor()
    {
        var query = "matrix " + Guid.NewGuid().ToString("N")[..6];
        StubSearch("movie", query, totalPages: 3, new { id = 603, title = "Matrix", original_title = "The Matrix", release_date = "1999-03-30", poster_path = "/m.jpg" });

        var body = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=movie", Ct);

        var hit = body.GetProperty("items")[0];
        hit.GetProperty("ref").GetString().Should().Be("tmdb:movie:603");
        hit.GetProperty("title").GetString().Should().Be("Matrix");
        hit.GetProperty("subtitle").GetString().Should().Be("The Matrix");
        hit.GetProperty("year").GetInt32().Should().Be(1999);
        hit.GetProperty("coverUrl").GetString().Should().Be("https://image.tmdb.org/t/p/w500/m.jpg");
        body.GetProperty("nextCursor").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Import_creates_the_item_once_and_search_then_shows_its_id()
    {
        var tmdbId = Random.Shared.Next(1_000_000, 9_000_000);
        StubMovie(tmdbId, "Dune: Çöl Gezegeni", overview: "Türkçe özet");
        var client = await factory.CreateSignedInClientAsync();

        var first = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"tmdb:movie:{tmdbId}" }, Ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        item.GetProperty("type").GetString().Should().Be("movie");
        item.GetProperty("creators")[0].GetString().Should().Be("Denis Villeneuve");
        item.GetProperty("genres")[0].GetString().Should().Be("Bilim-Kurgu");
        item.GetProperty("details").GetProperty("runtimeMinutes").GetInt32().Should().Be(155);
        first.Headers.Location!.ToString().Should().Be($"/api/v1/items/{item.GetProperty("id").GetString()}");

        var second = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"tmdb:movie:{tmdbId}" }, Ct);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString().Should().Be(item.GetProperty("id").GetString());

        var query = "dune " + tmdbId;
        StubSearch("movie", query, totalPages: 1, new { id = tmdbId, title = "Dune", release_date = "2021-09-15" });
        var search = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=movie", Ct);
        search.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(item.GetProperty("id").GetString());
        search.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Concurrent_imports_of_the_same_item_converge_on_one_row()
    {
        var tmdbId = Random.Shared.Next(1_000_000, 9_000_000);
        StubMovie(tmdbId, "Arrival", overview: "x");
        var client = await factory.CreateSignedInClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"tmdb:movie:{tmdbId}" }, Ct)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.OK);
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()));
        ids.Distinct(StringComparer.Ordinal).Should().ContainSingle();
    }

    [Fact]
    public async Task Series_import_falls_back_to_the_english_overview_and_counts_episodes()
    {
        var tmdbId = Random.Shared.Next(1_000_000, 9_000_000);
        factory.Providers.Given(Request.Create().WithPath($"/tmdb/3/tv/{tmdbId}").WithParam("language", "tr-TR").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                id = tmdbId,
                name = "Kara Para Aşk",
                original_name = "Kara Para Aşk",
                first_air_date = "2014-03-04",
                overview = "",
                number_of_episodes = 54,
                number_of_seasons = 2,
                genres = new[] { new { name = "Dram" } },
                created_by = Array.Empty<object>(),
            }));
        factory.Providers.Given(Request.Create().WithPath($"/tmdb/3/tv/{tmdbId}").WithParam("language", "en-US").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new { id = tmdbId, overview = "English overview" }));
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"tmdb:tv:{tmdbId}" }, Ct);

        var item = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        item.GetProperty("type").GetString().Should().Be("series");
        item.GetProperty("description").GetString().Should().Be("English overview");
        item.GetProperty("progressTotal").GetInt32().Should().Be(54);
        item.GetProperty("details").GetProperty("seasonCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Importing_an_unknown_id_is_422_and_a_bad_ref_is_400()
    {
        var client = await factory.CreateSignedInClientAsync();
        var missing = Random.Shared.Next(1_000_000, 9_000_000);
        factory.Providers.Given(Request.Create().WithPath($"/tmdb/3/movie/{missing}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var notFound = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"tmdb:movie:{missing}" }, Ct);
        await SignInTestsShouldBe(notFound, HttpStatusCode.UnprocessableEntity, "external_item_not_found");

        var malformed = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = "netflix:movie:1" }, Ct);
        var problem = await SignInTestsShouldBe(malformed, HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("errors").GetProperty("ref")[0].GetString().Should().Be("ref_invalid");
    }

    [Fact]
    public async Task Provider_outage_is_a_503_with_retry_after()
    {
        var query = "outage " + Guid.NewGuid().ToString("N")[..6];
        factory.Providers.Given(Request.Create().WithPath("/tmdb/3/search/tv").WithParam("query", query).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var response = await factory.CreateApiClient().GetAsync($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=series", Ct);

        await SignInTestsShouldBe(response, HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Search_validates_its_parameters()
    {
        var client = factory.CreateApiClient();

        var tooShort = await client.GetAsync("/api/v1/search?q=a&type=movie", Ct);
        (await SignInTestsShouldBe(tooShort, HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors").TryGetProperty("q", out _).Should().BeTrue();

        var badCursor = await client.GetAsync("/api/v1/search?q=abc&type=movie&cursor=garbage", Ct);
        await SignInTestsShouldBe(badCursor, HttpStatusCode.BadRequest, "invalid_cursor");
    }

    [Fact]
    public async Task Items_are_readable_anonymously_and_unknown_ids_are_404()
    {
        var tmdbId = Random.Shared.Next(1_000_000, 9_000_000);
        StubMovie(tmdbId, "Interstellar", overview: "x");
        var imported = await (await factory.CreateSignedInClientAsync()).PostAsJsonAsync("/api/v1/items/import", new { @ref = $"tmdb:movie:{tmdbId}" }, Ct);
        var id = (await imported.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString();
        var anonymous = factory.CreateApiClient();

        (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/items/{id}", Ct)).GetProperty("title").GetString().Should().Be("Interstellar");
        await SignInTestsShouldBe(await anonymous.GetAsync($"/api/v1/items/{Guid.NewGuid()}", Ct), HttpStatusCode.NotFound, "item_not_found");
    }

    private void StubSearch(string kind, string query, int totalPages, params object[] results) =>
        factory.Providers.Given(Request.Create().WithPath($"/tmdb/3/search/{kind}").WithParam("query", query).UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new { page = 1, total_pages = totalPages, results }));

    private void StubMovie(int id, string title, string overview) =>
        factory.Providers.Given(Request.Create().WithPath($"/tmdb/3/movie/{id}").WithParam("language", "tr-TR").UsingGet()
                .WithHeader("Authorization", "Bearer test-token"))
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                id,
                title,
                original_title = "Dune",
                release_date = "2021-09-15",
                overview,
                poster_path = "/d.jpg",
                runtime = 155,
                genres = new[] { new { name = "Bilim-Kurgu" } },
                credits = new { crew = new[] { new { job = "Director", name = "Denis Villeneuve" }, new { job = "Writer", name = "Jon Spaihts" } } },
            }));

    private static Task<JsonElement> SignInTestsShouldBe(HttpResponseMessage response, HttpStatusCode status, string code) =>
        Identity.SignInTests.ShouldBeProblem(response, status, code);
}
