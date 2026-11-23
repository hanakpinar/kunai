using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Catalog;

public sealed class ItemTypesTests(KunaiApiFactory factory)
{
    [Fact]
    public async Task Item_types_are_public_cacheable_and_localized()
    {
        var response = await factory.CreateApiClient().GetAsync("/api/v1/item-types", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.Should().BeTrue();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var book = body.GetProperty("types").EnumerateArray().Single(t => string.Equals(t.GetProperty("type").GetString(), "book", StringComparison.Ordinal));
        book.GetProperty("progressUnit").GetString().Should().Be("page");
        var reading = book.GetProperty("statuses").EnumerateArray().Single(s => string.Equals(s.GetProperty("status").GetString(), "inProgress", StringComparison.Ordinal));
        reading.GetProperty("label").GetProperty("tr").GetString().Should().Be("Okuyorum");
        reading.GetProperty("label").GetProperty("en").GetString().Should().Be("Reading");

        body.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("source").GetString())
            .Should().Contain(["tmdb", "googleBooks", "igdb", "aniList"]);
    }
}
