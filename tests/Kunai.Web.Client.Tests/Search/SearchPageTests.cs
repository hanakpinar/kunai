using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Search;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Search;

public sealed class SearchPageTests : KunaiTestContext
{
    private static readonly Guid DuneId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-3b2c4d5e6f70");

    [Fact]
    public void Hits_render_as_cards_and_more_asks_for_the_next_cursor()
    {
        Api.On(HttpMethod.Get, "/api/v1/search", new
        {
            items = new object[]
            {
                new { @ref = "openLibrary:OL893415W", id = DuneId, type = "book", title = "Dune", year = 1965, subtitle = "Frank Herbert", coverUrl = "https://covers.example/dune.jpg" },
                new { @ref = "openLibrary:OL45804W", id = (Guid?)null, type = "book", title = "Fahrenheit 451", year = 1953, subtitle = "Ray Bradbury", coverUrl = (string?)null },
            },
            nextCursor = "page-2",
        });
        var page = RenderSearch("search?q=dune&type=book");

        page.WaitForAssertion(() => page.FindAll(".item-card").Count.Should().Be(2));
        var first = page.FindAll(".item-card")[0];
        first.QuerySelector(".item-card__title")!.TextContent.Should().Be("Dune");
        first.QuerySelector(".item-card__meta")!.TextContent.Should().Contain("1965").And.Contain("Frank Herbert");
        first.QuerySelector("a.item-card__link")!.GetAttribute("href").Should().Be($"items/{DuneId}");
        page.FindAll("a.item-card__link")[1].GetAttribute("href").Should().Be("items/open?ref=openLibrary%3AOL45804W");
        page.Find("a[href='items/new']").TextContent.Should().Contain("Elle ekle");
        first.QuerySelector(".item-card__actions a")!.GetAttribute("href").Should().Be("login?returnUrl=search%3Fq%3Ddune%26type%3Dbook");

        Api.On(HttpMethod.Get, "/api/v1/search", new
        {
            items = new[] { new { @ref = "openLibrary:OL1W", id = (Guid?)null, type = "book", title = "Dune Messiah", year = 1969, subtitle = "Frank Herbert", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        page.FindAll("button").Single(b => b.TextContent.Contains("Daha fazla", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() => page.FindAll(".item-card").Count.Should().Be(3));
        var searches = Api.Requests.Where(r => r.PathAndQuery.StartsWith("/api/v1/search", StringComparison.Ordinal)).Select(r => r.PathAndQuery).ToList();
        searches.Should().HaveCount(2);
        searches[0].Should().ContainEquivalentOf("q=dune").And.ContainEquivalentOf("type=book").And.NotContainEquivalentOf("cursor");
        searches[1].Should().ContainEquivalentOf("cursor=page-2");
        page.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Daha fazla", StringComparison.Ordinal));
    }

    [Fact]
    public void Adding_a_hit_without_an_id_imports_it_then_saves_the_shelf_entry()
    {
        SignInAs("ninja");
        Api.On(HttpMethod.Get, "/api/v1/search", new
        {
            items = new[] { new { @ref = "tmdb:movie:438631", id = (Guid?)null, type = "movie", title = "Dune", year = 2021, subtitle = "Denis Villeneuve", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        Api.On(HttpMethod.Post, "/api/v1/items/import", ItemJson.Movie(DuneId), HttpStatusCode.Created);
        Api.On(HttpMethod.Put, $"/api/v1/me/shelf/{DuneId}", new
        {
            item = new { id = DuneId, type = "movie", title = "Dune", year = 2021, coverUrl = (string?)null, creators = new[] { "Denis Villeneuve" }, progressTotal = (int?)null },
            status = "planned",
            rating = (int?)null,
            startedOn = (string?)null,
            finishedOn = (string?)null,
            progress = (int?)null,
            review = (string?)null,
            isFavorite = false,
            createdAt = DateTimeOffset.UtcNow,
            updatedAt = DateTimeOffset.UtcNow,
        }, HttpStatusCode.Created);
        var page = RenderSearch("search?q=dune&type=movie");

        page.WaitForAssertion(() => page.Find(".item-card__actions button").TextContent.Should().Contain("Rafa ekle"));
        page.Find(".item-card__actions button").Click();
        page.WaitForAssertion(() => page.Find("section.dialog form"));
        page.Find("section.dialog form").Submit();

        page.WaitForAssertion(() => page.Find(".item-card .status-badge").TextContent.Should().Be("İzleyeceğim"));
        Api.BodiesSentTo(HttpMethod.Post, "/api/v1/items/import").Single().Should().Be("{\"ref\":\"tmdb:movie:438631\"}");
        Api.BodiesSentTo(HttpMethod.Put, $"/api/v1/me/shelf/{DuneId}").Single().Should().Contain("\"status\":\"planned\"");
        page.Find("a.item-card__link").GetAttribute("href").Should().Be($"items/{DuneId}");
        page.FindAll("section.dialog").Should().BeEmpty();
    }

    [Fact]
    public void A_provider_outage_shows_the_turkish_message_and_retry_searches_again()
    {
        Api.OnProblem(HttpMethod.Get, "/api/v1/search", HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        var page = RenderSearch("search?q=zelda&type=game");

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("İçerik kaynağı şu an yanıt vermiyor"));
        page.Find("nav.tabs a.active").TextContent.Should().Be("Oyun");

        Api.On(HttpMethod.Get, "/api/v1/search", new
        {
            items = new[] { new { @ref = "igdb:1029", id = (Guid?)null, type = "game", title = "The Legend of Zelda: Ocarina of Time", year = 1998, subtitle = "Nintendo 64", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        page.FindAll("button").Single(b => b.TextContent.Contains("Tekrar dene", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() => page.Find(".item-card__title").TextContent.Should().Contain("Ocarina of Time"));
        page.FindAll(".alert--error").Should().BeEmpty();
        Api.Requests.Count(r => r.PathAndQuery.StartsWith("/api/v1/search", StringComparison.Ordinal)).Should().Be(2);
    }

    private IRenderedComponent<SearchPage> RenderSearch(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
        return Render<SearchPage>();
    }
}
