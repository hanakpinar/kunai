using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Kunai.Api.IntegrationTests.Catalog;

public sealed class IgdbProviderTests(KunaiApiFactory factory)
{
    private const string GamesPath = "/igdb/v4/games";
    private const string TokenPath = "/twitch/oauth2/token";
    private const string AccessToken = "igdb-test-token";

    // A fixed id, so every test replaces the token stub instead of stacking copies.
    private static readonly Guid TokenMappingId = new("6f0c9a52-2f0e-4b7e-9a51-3d6c1f1e0a01");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_maps_games_to_hits_and_pages_by_offset()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var query = $"halo \"{suffix}\"";
        var searchClause = $"search \"halo \\\"{suffix}\\\"\";";
        var firstPage = new List<object>
        {
            new
            {
                id = 740,
                name = " Halo: Combat Evolved ",
                first_release_date = 1_005_782_400,
                cover = new { id = 85_001, image_id = "co2r2r" },
                platforms = new object[]
                {
                    new { id = 11, abbreviation = "XBOX", name = "Xbox" },
                    new { id = 14, name = "Mac" },
                    new { id = 6, abbreviation = "PC", name = "PC (Microsoft Windows)" },
                    new { id = 6, abbreviation = "PC", name = "PC (Microsoft Windows)" },
                    new { id = 12, abbreviation = "X360", name = "Xbox 360" },
                    new { id = 49, abbreviation = "XONE", name = "Xbox One" },
                },
            },
        };
        firstPage.AddRange(Enumerable.Range(1, 19).Select(i => (object)new { id = 10_000 + i, name = $"Halo {i}" }));
        StubGames(
            body => body.Contains(searchClause, StringComparison.Ordinal)
                && body.Contains("limit 20;", StringComparison.Ordinal)
                && body.Contains("offset 0;", StringComparison.Ordinal),
            firstPage);
        StubGames(
            body => body.Contains(searchClause, StringComparison.Ordinal) && body.Contains("offset 20;", StringComparison.Ordinal),
            new[] { new { id = 50_000, name = "Halo Wars" } });
        var client = factory.CreateApiClient();

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=game", Ct);

        first.GetProperty("items").GetArrayLength().Should().Be(20);
        var hit = first.GetProperty("items")[0];
        hit.GetProperty("ref").GetString().Should().Be("igdb:game:740");
        hit.GetProperty("type").GetString().Should().Be("game");
        hit.GetProperty("title").GetString().Should().Be("Halo: Combat Evolved");
        hit.GetProperty("year").GetInt32().Should().Be(2001);
        hit.GetProperty("subtitle").GetString().Should().Be("XBOX, Mac, PC, X360");
        hit.GetProperty("coverUrl").GetString().Should().Be("https://images.igdb.com/igdb/image/upload/t_cover_big/co2r2r.jpg");
        var cursor = first.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrEmpty();

        var second = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/search?q={Uri.EscapeDataString(query)}&type=game&cursor={Uri.EscapeDataString(cursor!)}", Ct);

        var last = second.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        last.GetProperty("ref").GetString().Should().Be("igdb:game:50000");
        last.GetProperty("year").ValueKind.Should().Be(JsonValueKind.Null);
        last.GetProperty("subtitle").ValueKind.Should().Be(JsonValueKind.Null);
        last.GetProperty("coverUrl").ValueKind.Should().Be(JsonValueKind.Null);
        second.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Import_maps_the_game_once_and_repeats_return_the_same_item()
    {
        var igdbId = Random.Shared.Next(1_000_000, 9_000_000);
        StubGames(
            body => body.Contains($"where id = {igdbId};", StringComparison.Ordinal)
                && body.Contains("involved_companies.company.name", StringComparison.Ordinal),
            new[]
            {
                new
                {
                    id = igdbId,
                    name = "The Witcher 3: Wild Hunt",
                    first_release_date = 1_431_993_600,
                    summary = "<p>Geralt &amp; Ciri.</p><p>The <b>Wild Hunt</b>.</p>",
                    cover = new { id = 89_386, image_id = "co1wyy" },
                    genres = new[] { new { id = 12, name = "Role-playing (RPG)" }, new { id = 31, name = " Adventure " }, new { id = 31, name = "Adventure" } },
                    platforms = new[] { new { id = 6, name = "PC (Microsoft Windows)" }, new { id = 48, name = "PlayStation 4" } },
                    involved_companies = new[]
                    {
                        new { id = 1, developer = true, company = new { id = 908, name = "CD Projekt RED" } },
                        new { id = 2, developer = false, company = new { id = 248, name = "Bandai Namco Entertainment" } },
                        new { id = 3, developer = true, company = new { id = 908, name = "CD Projekt RED" } },
                    },
                },
            });
        var client = await factory.CreateSignedInClientAsync();

        var first = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"igdb:game:{igdbId}" }, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        item.GetProperty("ref").GetString().Should().Be($"igdb:game:{igdbId}");
        item.GetProperty("type").GetString().Should().Be("game");
        item.GetProperty("title").GetString().Should().Be("The Witcher 3: Wild Hunt");
        item.GetProperty("year").GetInt32().Should().Be(2015);
        item.GetProperty("description").GetString().Should().Be("Geralt & Ciri.\nThe Wild Hunt.");
        item.GetProperty("coverUrl").GetString().Should().Be("https://images.igdb.com/igdb/image/upload/t_cover_big/co1wyy.jpg");
        item.GetProperty("creators").EnumerateArray().Select(c => c.GetString()).Should().Equal("CD Projekt RED");
        item.GetProperty("genres").EnumerateArray().Select(g => g.GetString()).Should().Equal("Role-playing (RPG)", "Adventure");
        item.GetProperty("details").GetProperty("platforms").EnumerateArray().Select(p => p.GetString())
            .Should().Equal("PC (Microsoft Windows)", "PlayStation 4");
        item.GetProperty("progressTotal").ValueKind.Should().Be(JsonValueKind.Null);

        var second = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"igdb:game:{igdbId}" }, Ct);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString().Should().Be(item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Importing_an_unknown_game_is_422()
    {
        var missing = Random.Shared.Next(1_000_000, 9_000_000);
        StubGames(body => body.Contains($"where id = {missing};", StringComparison.Ordinal), Array.Empty<object>());
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"igdb:game:{missing}" }, Ct);

        await Identity.SignInTests.ShouldBeProblem(response, HttpStatusCode.UnprocessableEntity, "external_item_not_found");
    }

    [Fact]
    public async Task Igdb_outage_on_search_is_a_503_with_retry_after()
    {
        var query = "outage " + Guid.NewGuid().ToString("N")[..8];
        StubToken();
        factory.Providers.Given(GamesRequest(body => body.Contains($"search \"{query}\";", StringComparison.Ordinal)))
            .RespondWith(Response.Create().WithStatusCode(500));

        var response = await factory.CreateApiClient().GetAsync($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=game", Ct);

        await Identity.SignInTests.ShouldBeProblem(response, HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task A_rejected_token_is_renewed_and_the_request_retried_once()
    {
        var query = "portal " + Guid.NewGuid().ToString("N")[..8];
        var scenario = $"igdb-401-{query}";
        StubToken();
        factory.Providers.Given(GamesRequest(body => body.Contains(query, StringComparison.Ordinal)))
            .InScenario(scenario)
            .WillSetStateTo("token-renewed")
            .RespondWith(Response.Create().WithStatusCode(401));
        factory.Providers.Given(GamesRequest(body => body.Contains(query, StringComparison.Ordinal)))
            .InScenario(scenario)
            .WhenStateIs("token-renewed")
            .RespondWith(Response.Create().WithBodyAsJson(new[] { new { id = 71, name = "Portal" } }));
        var tokenCallsBefore = CountRequests(TokenPath);

        var body = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=game", Ct);

        body.GetProperty("items")[0].GetProperty("ref").GetString().Should().Be("igdb:game:71");
        CountRequests(GamesPath, query).Should().Be(2);
        CountRequests(TokenPath).Should().BeGreaterThan(tokenCallsBefore);
    }

    private void StubGames(Func<string, bool> body, object games)
    {
        StubToken();
        factory.Providers.Given(GamesRequest(body)).RespondWith(Response.Create().WithBodyAsJson(games));
    }

    private static IRequestBuilder GamesRequest(Func<string, bool> body) =>
        Request.Create()
            .WithPath(GamesPath)
            .UsingPost()
            .WithHeader("Client-ID", "test-client")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("Content-Type", "text/plain*")
            .WithBody((string? actual) => actual is not null && body(actual));

    private void StubToken() =>
        factory.Providers.Given(Request.Create()
                .WithPath(TokenPath)
                .UsingPost()
                .WithParam("client_id", "test-client")
                .WithParam("client_secret", "test-secret")
                .WithParam("grant_type", "client_credentials"))
            .WithGuid(TokenMappingId)
            .RespondWith(Response.Create().WithBodyAsJson(new { access_token = AccessToken, expires_in = 5_587_808, token_type = "bearer" }));

    private int CountRequests(string path, string? bodyContains = null) =>
        factory.Providers.LogEntries.Count(entry =>
            entry.RequestMessage is { } request
            && string.Equals(request.Path, path, StringComparison.Ordinal)
            && (bodyContains is null || (request.Body?.Contains(bodyContains, StringComparison.Ordinal) ?? false)));
}
