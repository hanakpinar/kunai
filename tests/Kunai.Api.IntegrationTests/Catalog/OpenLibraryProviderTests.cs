using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Kunai.Api.IntegrationTests.Catalog;

public sealed class OpenLibraryProviderTests(KunaiApiFactory factory)
{
    private const string SearchFields = "key,title,author_name,first_publish_year,cover_i,number_of_pages_median";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Book_search_maps_works_to_refs_authors_years_https_covers_and_pages()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var query = "dune " + token;
        var workId = NewWorkId();
        FailGoogleBooksFor(token);
        StubSearch(query, page: 1, numFound: 25,
            new
            {
                key = $"/works/{workId}",
                title = " Dune ",
                author_name = new[] { "Frank Herbert", " Brian Herbert ", "Frank Herbert", "Kevin J. Anderson", "Fourth Author" },
                first_publish_year = 1965,
                cover_i = 11481354,
                number_of_pages_median = 604,
            },
            new { key = "/works/OL2W", title = "No cover, no year", first_publish_year = 0 },
            new { key = "/books/OL1M", title = "An edition, not a work" },
            new { key = "/works/OL3W" });
        StubSearch(query, page: 2, numFound: 25, new { key = "/works/OL4W", title = "Last page" });
        var client = factory.CreateApiClient();

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=book", Ct);

        body.GetProperty("items").GetArrayLength().Should().Be(2);
        var hit = body.GetProperty("items")[0];
        hit.GetProperty("ref").GetString().Should().Be($"olib:work:{workId}");
        hit.GetProperty("type").GetString().Should().Be("book");
        hit.GetProperty("title").GetString().Should().Be("Dune");
        hit.GetProperty("year").GetInt32().Should().Be(1965);
        hit.GetProperty("subtitle").GetString().Should().Be("Frank Herbert, Brian Herbert, Kevin J. Anderson");
        hit.GetProperty("coverUrl").GetString().Should().Be("https://covers.openlibrary.org/b/id/11481354-L.jpg");
        var bare = body.GetProperty("items")[1];
        bare.GetProperty("ref").GetString().Should().Be("olib:work:OL2W");
        bare.GetProperty("year").ValueKind.Should().Be(JsonValueKind.Null);
        bare.GetProperty("subtitle").ValueKind.Should().Be(JsonValueKind.Null);
        bare.GetProperty("coverUrl").ValueKind.Should().Be(JsonValueKind.Null);

        var cursor = body.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrEmpty();
        var last = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=book&cursor={cursor}", Ct);
        last.GetProperty("items")[0].GetProperty("title").GetString().Should().Be("Last page");
        last.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);

        // WireMock splits comma-separated parameter values, so check the field list on the raw query.
        factory.Providers.FindLogEntries(Request.Create().WithPath("/olib/search.json").WithParam("q", query).UsingGet())
            .Should().OnlyContain(entry => entry.RequestMessage!.RawQuery!.Contains("fields=" + SearchFields, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_maps_the_work_and_its_authors_once_and_is_idempotent()
    {
        var workId = NewWorkId();
        var (frank, brian) = ($"OL{Random.Shared.Next(1_000_000, 9_000_000)}A", $"OL{Random.Shared.Next(1_000_000, 9_000_000)}A");
        StubWork(workId, new
        {
            key = $"/works/{workId}",
            title = "Dune",
            type = new { key = "/type/work" },
            description = new { type = "/type/text", value = "<p>Paul &amp; the <b>spice</b>.</p>\r\n<br/>Second line &lt;i&gt;x&lt;/i&gt;" },
            covers = new[] { -1, 11481354 },
            first_publish_date = "August 1965",
            subjects = new[] { "Science fiction", " Fiction ", "Science fiction", "nyt:mass-market-monthly=2021-11-07", "award:nebula_award=novel", "Dune (Imaginary place)" },
            authors = new[]
            {
                new { author = new { key = $"/authors/{frank}" }, type = new { key = "/type/author_role" } },
                new { author = new { key = $"/authors/{brian}" }, type = new { key = "/type/author_role" } },
                new { author = new { key = $"/authors/{frank}" }, type = new { key = "/type/author_role" } },
            },
        });
        StubAuthor(frank, "Frank Herbert");
        StubAuthor(brian, "Brian Herbert");
        var client = await factory.CreateSignedInClientAsync();

        var first = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"olib:work:{workId}" }, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        item.GetProperty("type").GetString().Should().Be("book");
        item.GetProperty("ref").GetString().Should().Be($"olib:work:{workId}");
        item.GetProperty("title").GetString().Should().Be("Dune");
        item.GetProperty("year").GetInt32().Should().Be(1965);
        item.GetProperty("description").GetString().Should().Be("Paul & the spice.\n\nSecond line x");
        item.GetProperty("coverUrl").GetString().Should().Be("https://covers.openlibrary.org/b/id/11481354-L.jpg");
        item.GetProperty("creators").EnumerateArray().Select(c => c.GetString()).Should().Equal("Frank Herbert", "Brian Herbert");
        item.GetProperty("genres").EnumerateArray().Select(g => g.GetString()).Should().Equal("Science fiction", "Fiction", "Dune (Imaginary place)");
        item.GetProperty("progressTotal").ValueKind.Should().Be(JsonValueKind.Null);
        factory.Providers.FindLogEntries(Request.Create().WithPath($"/olib/authors/{frank}.json").UsingGet()).Should().ContainSingle();

        var second = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"olib:work:{workId}" }, Ct);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString().Should().Be(item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Import_follows_a_merged_work_and_reads_a_plain_string_description()
    {
        var (oldId, newId) = (NewWorkId(), NewWorkId());
        StubWork(oldId, new { key = $"/works/{oldId}", type = new { key = "/type/redirect" }, location = $"/works/{newId}" });
        StubWork(newId, new
        {
            key = $"/works/{newId}",
            title = "Children of Dune",
            type = new { key = "/type/work" },
            description = "The third &quot;Dune&quot; novel.",
            covers = new[] { -1 },
        });
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"olib:work:{oldId}" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        item.GetProperty("ref").GetString().Should().Be($"olib:work:{oldId}");
        item.GetProperty("title").GetString().Should().Be("Children of Dune");
        item.GetProperty("description").GetString().Should().Be("The third \"Dune\" novel.");
        item.GetProperty("year").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("coverUrl").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("creators").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Importing_an_unknown_or_malformed_work_is_422()
    {
        var missing = NewWorkId();
        factory.Providers.Given(Request.Create().WithPath($"/olib/works/{missing}.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));
        var client = await factory.CreateSignedInClientAsync();

        foreach (var reference in new[] { $"olib:work:{missing}", "olib:work:not-a-work-id", $"olib:edition:{missing}" })
        {
            var response = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = reference }, Ct);
            await ShouldBeProblem(response, HttpStatusCode.UnprocessableEntity, "external_item_not_found");
        }
    }

    [Fact]
    public async Task Open_library_outage_on_search_is_a_503_with_retry_after()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var query = "outage " + token;
        FailGoogleBooksFor(token);
        factory.Providers.Given(Request.Create().WithPath("/olib/search.json").WithParam("q", query).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var response = await factory.CreateApiClient().GetAsync($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=book", Ct);

        await ShouldBeProblem(response, HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
    }

    private static string NewWorkId() => $"OL{Random.Shared.Next(1_000_000, 90_000_000)}W";

    /// <summary>Google Books is searched before Open Library; make it fail for this query so the fallback answers.</summary>
    private void FailGoogleBooksFor(string token) =>
        factory.Providers.Given(Request.Create().WithPath(new WildcardMatcher("/gbooks/*")).WithParam("q", new WildcardMatcher($"*{token}*")).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

    private void StubSearch(string query, int page, int numFound, params object[] docs) =>
        factory.Providers.Given(Request.Create().WithPath("/olib/search.json")
                .WithParam("q", query)
                .WithParam("page", page.ToString(CultureInfo.InvariantCulture))
                .WithParam("limit", "20")
                .WithHeader("User-Agent", "Kunai/1.0")
                .UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new { numFound, num_found = numFound, start = (page - 1) * 20, docs }));

    private void StubWork(string id, object work) =>
        factory.Providers.Given(Request.Create().WithPath($"/olib/works/{id}.json").WithHeader("User-Agent", "Kunai/1.0").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(work));

    private void StubAuthor(string id, string name) =>
        factory.Providers.Given(Request.Create().WithPath($"/olib/authors/{id}.json").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new { key = $"/authors/{id}", name }));

    private static Task<JsonElement> ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string code) =>
        Identity.SignInTests.ShouldBeProblem(response, status, code);
}
