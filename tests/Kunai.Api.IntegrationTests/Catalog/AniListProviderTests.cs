using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Kunai.Api.IntegrationTests.Catalog;

/// <remarks>
/// The client-side limit (30/min) hands out 5 AniList calls per 10 seconds and a queued call counts against
/// the 5-second attempt timeout, so this class makes at most 5 calls in total (a 5xx search costs 2 with the retry).
/// </remarks>
public sealed class AniListProviderTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Anime_search_maps_titles_years_studios_and_https_covers()
    {
        var query = "bebop " + Guid.NewGuid().ToString("N")[..6];
        var (first, second) = (Random.Shared.Next(1_000_000, 9_000_000), Random.Shared.Next(1_000_000, 9_000_000));
        StubSearch("ANIME", query, hasNextPage: true,
            new
            {
                id = first,
                title = new { romaji = "Cowboy Bebop", english = "Cowboy Bebop", native = "カウボーイビバップ" },
                startDate = new { year = 1998 },
                coverImage = new { large = "http://s4.anilist.co/file/anilistcdn/media/anime/cover/medium/bx1.jpg" },
                format = "TV",
                episodes = 26,
                studios = new { nodes = new[] { new { name = " Sunrise " }, new { name = "Sunrise" }, new { name = "Bandai Visual" }, new { name = "Third" } } },
                staff = new { nodes = new[] { new { name = new { full = "Shinichirou Watanabe" } } } },
            },
            new
            {
                id = second,
                title = new { romaji = "Cowboy Bebop: Tengoku no Tobira", english = (string?)null, native = "カウボーイビバップ 天国の扉" },
                startDate = new { year = (int?)null },
                coverImage = (object?)null,
                format = "MOVIE",
                episodes = 1,
                studios = new { nodes = Array.Empty<object>() },
                staff = new { nodes = Array.Empty<object>() },
            });

        var body = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=anime", Ct);

        var hits = body.GetProperty("items");
        hits.GetArrayLength().Should().Be(2);
        hits[0].GetProperty("ref").GetString().Should().Be($"anilist:anime:{first}");
        hits[0].GetProperty("type").GetString().Should().Be("anime");
        hits[0].GetProperty("title").GetString().Should().Be("Cowboy Bebop");
        hits[0].GetProperty("year").GetInt32().Should().Be(1998);
        hits[0].GetProperty("subtitle").GetString().Should().Be("Sunrise, Bandai Visual");
        hits[0].GetProperty("coverUrl").GetString().Should().Be("https://s4.anilist.co/file/anilistcdn/media/anime/cover/medium/bx1.jpg");
        hits[1].GetProperty("ref").GetString().Should().Be($"anilist:anime:{second}");
        hits[1].GetProperty("title").GetString().Should().Be("Cowboy Bebop: Tengoku no Tobira");
        hits[1].GetProperty("year").ValueKind.Should().Be(JsonValueKind.Null);
        hits[1].GetProperty("subtitle").ValueKind.Should().Be(JsonValueKind.Null);
        hits[1].GetProperty("coverUrl").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("nextCursor").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Manga_import_maps_the_details_and_is_idempotent()
    {
        var id = Random.Shared.Next(1_000_000, 9_000_000);
        StubMedia("MANGA", id, Response.Create().WithBodyAsJson(new
        {
            data = new
            {
                Media = new
                {
                    id,
                    title = new { romaji = "Berserk", english = "Berserk", native = "ベルセルク" },
                    startDate = new { year = 1989 },
                    coverImage = new { extraLarge = "https://s4.anilist.co/file/anilistcdn/media/manga/cover/large/bx30002.jpg", large = "https://s4.anilist.co/m.jpg" },
                    description = "Guts, a former mercenary&#039;s tale.<br><br>\n<i>The Black Swordsman</i> &amp; the Band of the Hawk.",
                    genres = new[] { "Action", " Drama ", "Action" },
                    format = "MANGA",
                    episodes = (int?)null,
                    chapters = 380,
                    volumes = 42,
                    studios = new { nodes = Array.Empty<object>() },
                    staff = new { nodes = new[] { new { name = new { full = "Kentarou Miura" } }, new { name = new { full = "Studio Gaga" } }, new { name = new { full = "Kentarou Miura" } } } },
                },
            },
        }));
        var client = await factory.CreateSignedInClientAsync();

        var first = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"anilist:manga:{id}" }, Ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        item.GetProperty("type").GetString().Should().Be("manga");
        item.GetProperty("ref").GetString().Should().Be($"anilist:manga:{id}");
        item.GetProperty("title").GetString().Should().Be("Berserk");
        item.GetProperty("originalTitle").GetString().Should().Be("ベルセルク");
        item.GetProperty("year").GetInt32().Should().Be(1989);
        item.GetProperty("description").GetString().Should().Be("Guts, a former mercenary's tale.\n\nThe Black Swordsman & the Band of the Hawk.");
        item.GetProperty("coverUrl").GetString().Should().Be("https://s4.anilist.co/file/anilistcdn/media/manga/cover/large/bx30002.jpg");
        item.GetProperty("creators").EnumerateArray().Select(c => c.GetString()).Should().Equal("Kentarou Miura", "Studio Gaga");
        item.GetProperty("genres").EnumerateArray().Select(g => g.GetString()).Should().Equal("Action", "Drama");
        item.GetProperty("progressTotal").GetInt32().Should().Be(380);
        item.GetProperty("details").GetProperty("format").GetString().Should().Be("MANGA");
        item.GetProperty("details").GetProperty("volumeCount").GetInt32().Should().Be(42);

        var second = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"anilist:manga:{id}" }, Ct);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString().Should().Be(item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Unknown_and_malformed_ids_are_422_external_item_not_found()
    {
        var missing = Random.Shared.Next(1_000_000, 9_000_000);
        var stub = StubMedia("ANIME", missing, Response.Create().WithStatusCode(404).WithBodyAsJson(new
        {
            errors = new[] { new { message = "Not Found.", status = 404 } },
            data = new { Media = (object?)null },
        }));
        var client = await factory.CreateSignedInClientAsync();

        // Only the first ref reaches AniList; the others are rejected before any call.
        foreach (var reference in new[] { $"anilist:anime:{missing}", "anilist:anime:abc", "anilist:anime:0021", "anilist:novel:21" })
        {
            var response = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = reference }, Ct);
            await ShouldBeProblem(response, HttpStatusCode.UnprocessableEntity, "external_item_not_found");
        }

        // WireMock also answers unmatched requests with 404, so prove the AniList-shaped stub was the one used.
        factory.Providers.LogEntries.Should().ContainSingle(entry => entry.MappingGuid == stub);
    }

    [Fact]
    public async Task Search_outage_is_a_503_with_retry_after()
    {
        var query = "outage " + Guid.NewGuid().ToString("N")[..6];
        factory.Providers.Given(GraphQlRequest(new { variables = new { search = query, type = "MANGA" } }))
            .RespondWith(Response.Create().WithStatusCode(500));

        var response = await factory.CreateApiClient().GetAsync($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=manga", Ct);

        await ShouldBeProblem(response, HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
    }

    private void StubSearch(string type, string query, bool hasNextPage, params object[] media) =>
        factory.Providers.Given(GraphQlRequest(new { variables = new { search = query, type, page = 1, perPage = 20 } }))
            .RespondWith(Response.Create().WithBodyAsJson(new { data = new { Page = new { pageInfo = new { hasNextPage }, media } } }));

    private Guid StubMedia(string type, int id, IResponseBuilder response)
    {
        var mapping = Guid.NewGuid();
        factory.Providers.Given(GraphQlRequest(new { variables = new { id, type } })).WithGuid(mapping).RespondWith(response);
        return mapping;
    }

    /// <summary>Every AniList call is a POST to the root; the variables tell the requests apart.</summary>
    private static IRequestBuilder GraphQlRequest(object partialBody) =>
        Request.Create().WithPath("/anilist/").UsingPost()
            .WithHeader("Accept", "application/json")
            .WithBody(new JsonPartialMatcher(partialBody));

    private static Task<JsonElement> ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string code) =>
        Identity.SignInTests.ShouldBeProblem(response, status, code);
}
