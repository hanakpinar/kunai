using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Kunai.Api.IntegrationTests.Catalog;

public sealed class GoogleBooksProviderTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Book_search_maps_volumes_to_hits_and_pages_by_twenty()
    {
        var query = "dune " + Guid.NewGuid().ToString("N")[..6];
        var id = NewVolumeId();
        StubSearch(query, startIndex: 0, totalItems: 21,
            new
            {
                id,
                volumeInfo = new
                {
                    title = "Dune",
                    subtitle = "Deluxe Edition",
                    authors = new[] { "Frank Herbert", " Frank Herbert ", "Brian Herbert" },
                    publishedDate = "2005-08-02",
                    imageLinks = new { smallThumbnail = Cover(id, zoom: 5), thumbnail = Cover(id, zoom: 1) },
                },
            },
            new { id = NewVolumeId(), volumeInfo = new { authors = new[] { "No Title" } } });
        StubSearch(query, startIndex: 20, totalItems: 21, new { id = NewVolumeId(), volumeInfo = new { title = "Dune Messiah", publishedDate = "19??" } });
        var client = factory.CreateApiClient();

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=book", Ct);

        first.GetProperty("items").GetArrayLength().Should().Be(1);
        var hit = first.GetProperty("items")[0];
        hit.GetProperty("ref").GetString().Should().Be($"gbooks:volume:{id}");
        hit.GetProperty("type").GetString().Should().Be("book");
        hit.GetProperty("title").GetString().Should().Be("Dune: Deluxe Edition");
        hit.GetProperty("year").GetInt32().Should().Be(2005);
        hit.GetProperty("subtitle").GetString().Should().Be("Frank Herbert, Brian Herbert");
        hit.GetProperty("coverUrl").GetString().Should().Be($"https://books.google.com/books/content?id={id}&printsec=frontcover&img=1&zoom=1&source=gbs_api");
        var cursor = first.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrEmpty();

        var second = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=book&cursor={cursor}", Ct);

        second.GetProperty("items")[0].GetProperty("title").GetString().Should().Be("Dune Messiah");
        second.GetProperty("items")[0].GetProperty("year").ValueKind.Should().Be(JsonValueKind.Null);
        second.GetProperty("items")[0].GetProperty("coverUrl").ValueKind.Should().Be(JsonValueKind.Null);
        second.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Import_maps_the_volume_and_is_idempotent()
    {
        var id = NewVolumeId();
        StubVolume(id);
        var client = await factory.CreateSignedInClientAsync();

        var first = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"gbooks:volume:{id}" }, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        item.GetProperty("type").GetString().Should().Be("book");
        item.GetProperty("ref").GetString().Should().Be($"gbooks:volume:{id}");
        item.GetProperty("source").GetString().Should().Be("googleBooks");
        item.GetProperty("title").GetString().Should().Be("Dune: Deluxe Edition");
        item.GetProperty("originalTitle").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("year").GetInt32().Should().Be(2005);
        item.GetProperty("description").GetString().Should().Be("A desert planet & its spice.\nBook one of the saga.");
        item.GetProperty("coverUrl").GetString().Should().Be($"https://books.google.com/books/content?id={id}&printsec=frontcover&img=1&zoom=1&source=gbs_api");
        item.GetProperty("creators").EnumerateArray().Select(c => c.GetString()).Should().Equal("Frank Herbert");
        item.GetProperty("genres").EnumerateArray().Select(g => g.GetString()).Should().Equal("Fiction / Science Fiction / General", "Fiction / Classics");
        item.GetProperty("progressTotal").GetInt32().Should().Be(896);
        item.GetProperty("details").GetProperty("isbn13").GetString().Should().Be("9780441013593");
        item.GetProperty("details").GetProperty("publisher").GetString().Should().Be("Penguin");

        var second = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"gbooks:volume:{id}" }, Ct);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString().Should().Be(item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Search_shows_the_catalog_id_of_an_imported_volume()
    {
        var id = NewVolumeId();
        StubVolume(id);
        var client = await factory.CreateSignedInClientAsync();
        var imported = await (await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"gbooks:volume:{id}" }, Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        var query = "herbert " + id;
        StubSearch(query, startIndex: 0, totalItems: 1, new { id, volumeInfo = new { title = "Dune" } });

        var search = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=book", Ct);

        search.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(imported.GetProperty("id").GetString());
        search.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Importing_an_unknown_or_malformed_volume_is_422()
    {
        var client = await factory.CreateSignedInClientAsync();
        var missing = NewVolumeId();
        factory.Providers.Given(Request.Create().WithPath($"/gbooks/v1/volumes/{missing}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404).WithBodyAsJson(new { error = new { code = 404, message = "The volume ID could not be found." } }));

        var notFound = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"gbooks:volume:{missing}" }, Ct);
        await ShouldBeProblem(notFound, HttpStatusCode.UnprocessableEntity, "external_item_not_found");

        var wrongKind = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = $"gbooks:movie:{missing}" }, Ct);
        await ShouldBeProblem(wrongKind, HttpStatusCode.UnprocessableEntity, "external_item_not_found");

        var badId = await client.PostAsJsonAsync("/api/v1/items/import", new { @ref = "gbooks:volume:..%2F..%2Fusers" }, Ct);
        await ShouldBeProblem(badId, HttpStatusCode.UnprocessableEntity, "external_item_not_found");
    }

    [Fact]
    public async Task Provider_outage_is_a_503_with_retry_after()
    {
        var query = "outage " + Guid.NewGuid().ToString("N")[..6];
        factory.Providers.Given(Request.Create().WithPath("/gbooks/v1/volumes").WithParam("q", query).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        var response = await factory.CreateApiClient().GetAsync($"/api/v1/search?q={Uri.EscapeDataString(query)}&type=book", Ct);

        await ShouldBeProblem(response, HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>Google Books ids are 12 URL-safe characters.</summary>
    private static string NewVolumeId() => "t" + Guid.NewGuid().ToString("N")[..11];

    private static string Cover(string id, int zoom) =>
        $"http://books.google.com/books/content?id={id}&printsec=frontcover&img=1&zoom={zoom}&edge=curl&source=gbs_api";

    private void StubSearch(string query, int startIndex, int totalItems, params object[] items) =>
        factory.Providers.Given(Request.Create().WithPath("/gbooks/v1/volumes")
                .WithParam("q", query)
                .WithParam("startIndex", startIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .WithParam("maxResults", "20")
                .WithParam("printType", "books")
                .UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new { kind = "books#volumes", totalItems, items }));

    private void StubVolume(string id) =>
        factory.Providers.Given(Request.Create().WithPath($"/gbooks/v1/volumes/{id}").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                kind = "books#volume",
                id,
                volumeInfo = new
                {
                    title = "Dune",
                    subtitle = "Deluxe Edition",
                    authors = new[] { "Frank Herbert", "Frank Herbert " },
                    publisher = " Penguin ",
                    publishedDate = "2005-08-02",
                    description = "<p>A <b>desert planet</b> &amp; its spice.<br>Book one of the <i>saga</i>.</p>",
                    industryIdentifiers = new[]
                    {
                        new { type = "ISBN_10", identifier = "0441013597" },
                        new { type = "ISBN_13", identifier = "9780441013593" },
                    },
                    pageCount = 896,
                    printType = "BOOK",
                    categories = new[] { "Fiction / Science Fiction / General", "Fiction / Classics", "Fiction / Classics" },
                    imageLinks = new { smallThumbnail = Cover(id, zoom: 5), thumbnail = Cover(id, zoom: 1), small = Cover(id, zoom: 2) },
                    language = "en",
                },
            }));

    private static Task<JsonElement> ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string code) =>
        Identity.SignInTests.ShouldBeProblem(response, status, code);
}
