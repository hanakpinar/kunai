using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Search;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Search;

public sealed class SearchPageTests : KunaiTestContext
{
    private const string SearchPath = "/api/v1/search";
    private static readonly Guid DuneId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-3b2c4d5e6f70");
    private static readonly Guid MadonnaId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-000000000160");

    [Fact]
    public void Hits_render_as_cards_and_more_appends_the_next_page_without_repeating_a_hit()
    {
        Api.On(HttpMethod.Get, SearchPath, new
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
        first.QuerySelector(".hit-hint").Should().BeNull("a hit in the catalog opens without an account");
        page.Find("a[href='items/new']").TextContent.Should().Contain("Elle ekle");
        first.QuerySelector(".item-card__actions a")!.GetAttribute("href").Should().Be("login?returnUrl=search%3Fq%3Ddune%26type%3Dbook");
        page.Find("form.search__form").GetAttribute("aria-label").Should().Be("Aramayı düzenle");

        // Opening a hit that is not in the catalog imports it, which needs an account: say so and go to sign-in first.
        var second = page.FindAll(".item-card")[1];
        second.QuerySelector("a.item-card__link")!.GetAttribute("href").Should().Be("login?returnUrl=items%2Fopen%3Fref%3DopenLibrary%253AOL45804W");
        second.QuerySelector(".hit-hint")!.TextContent.Should().Be("Ayrıntılar için giriş yap");

        // Provider pages are cached one by one, so page 2 can repeat a hit from page 1.
        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new object[]
            {
                new { @ref = "openLibrary:OL45804W", id = (Guid?)null, type = "book", title = "Fahrenheit 451", year = 1953, subtitle = "Ray Bradbury", coverUrl = (string?)null },
                new { @ref = "openLibrary:OL1W", id = (Guid?)null, type = "book", title = "Dune Messiah", year = 1969, subtitle = "Frank Herbert", coverUrl = (string?)null },
            },
            nextCursor = (string?)null,
        });
        page.FindAll("button").Single(b => HasText(b, "Daha fazla")).Click();

        page.WaitForAssertion(() => page.FindAll(".item-card__title").Select(t => t.TextContent).Should().Equal("Dune", "Fahrenheit 451", "Dune Messiah"));
        var searches = SearchRequests();
        searches.Should().HaveCount(2);
        searches[0].Should().ContainEquivalentOf("q=dune").And.ContainEquivalentOf("type=book").And.NotContainEquivalentOf("cursor");
        searches[1].Should().ContainEquivalentOf("cursor=page-2");
        page.FindAll("button").Should().NotContain(b => HasText(b, "Daha fazla"));
    }

    [Fact]
    public void Changing_the_type_drops_the_old_hits_and_searches_from_the_first_page()
    {
        Api.AnswerAsynchronously = true;
        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new[] { new { @ref = "openLibrary:OL893415W", id = (Guid?)null, type = "book", title = "Dune", year = 1965, subtitle = "Frank Herbert", coverUrl = (string?)null } },
            nextCursor = "page-2",
        });
        var page = RenderSearch("search?q=dune&type=book");
        page.WaitForAssertion(() => page.Find(".item-card__type").TextContent.Should().Be("Kitap"));

        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new[] { new { @ref = "tmdb:movie:438631", id = (Guid?)null, type = "movie", title = "Dune: Part One", year = 2021, subtitle = "Denis Villeneuve", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        Services.GetRequiredService<NavigationManager>().NavigateTo("search?q=dune&type=movie");

        page.WaitForAssertion(() => page.FindAll(".item-card__title").Select(t => t.TextContent).Should().Equal("Dune: Part One"));
        page.Find(".item-card__type").TextContent.Should().Be("Film");
        page.Find("nav.tabs a.active").TextContent.Should().Be("Film");
        page.FindAll("button").Should().NotContain(b => HasText(b, "Daha fazla"), "the book page's cursor belongs to the old search");
        page.FindAll(".search__manual").Should().BeEmpty("adding by hand is only for books");
        var searches = SearchRequests();
        searches.Should().HaveCount(2);
        searches[1].Should().ContainEquivalentOf("type=movie").And.ContainEquivalentOf("q=dune").And.NotContainEquivalentOf("cursor");
    }

    [Fact]
    public void A_query_over_100_characters_is_not_sent_and_says_why()
    {
        var page = RenderSearch($"search?q={new string('a', 101)}&type=book");

        page.WaitForAssertion(() => page.Find(".empty-state").TextContent.Should().Contain("en fazla 100 karakter"));
        SearchRequests().Should().BeEmpty();
        page.FindAll("button").Should().NotContain(b => HasText(b, "Tekrar dene"));
        page.Find(".search__manual a[href='items/new']").TextContent.Should().Be("Elle ekle");
    }

    [Fact]
    public void An_empty_book_search_offers_adding_by_hand_once()
    {
        Api.On(HttpMethod.Get, SearchPath, new { items = Array.Empty<object>(), nextCursor = (string?)null });
        var page = RenderSearch("search?q=kayip%20kitap&type=book");

        page.WaitForAssertion(() => page.Find(".empty-state").TextContent.Should().Contain("bir şey bulamadık"));
        page.FindAll("a[href='items/new']").Should().ContainSingle().Which.TextContent.Should().Be("Kitabı elle ekle");
    }

    [Fact]
    public void A_provider_outage_shows_the_turkish_message_and_retry_searches_again()
    {
        Api.OnProblem(HttpMethod.Get, SearchPath, HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        var page = RenderSearch("search?q=zelda&type=game");

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("İçerik kaynağı şu an yanıt vermiyor"));
        page.Find("nav.tabs a.active").TextContent.Should().Be("Oyun");
        page.FindAll(".search__manual").Should().BeEmpty();

        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new[] { new { @ref = "igdb:1029", id = (Guid?)null, type = "game", title = "The Legend of Zelda: Ocarina of Time", year = 1998, subtitle = "Nintendo 64", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        page.FindAll("button").Single(b => HasText(b, "Tekrar dene")).Click();

        page.WaitForAssertion(() => page.Find(".item-card__title").TextContent.Should().Contain("Ocarina of Time"));
        page.FindAll(".alert--error").Should().BeEmpty();
        SearchRequests().Should().HaveCount(2);
    }

    [Fact]
    public void A_failed_book_search_still_links_to_adding_the_book_by_hand()
    {
        Api.OnProblem(HttpMethod.Get, SearchPath, HttpStatusCode.ServiceUnavailable, "provider_unavailable");
        var page = RenderSearch("search?q=kurk%20mantolu&type=book");

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("İçerik kaynağı şu an yanıt vermiyor"));
        page.Find(".search__manual a[href='items/new']").TextContent.Should().Be("Elle ekle");
        page.FindAll("button").Should().Contain(b => HasText(b, "Tekrar dene"));
    }

    [Fact]
    public void A_failure_that_a_retry_cannot_fix_shows_no_retry_button()
    {
        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new[] { new { @ref = "igdb:1029", id = (Guid?)null, type = "game", title = "Ocarina of Time", year = 1998, subtitle = "Nintendo 64", coverUrl = (string?)null } },
            nextCursor = "page-2",
        });
        var page = RenderSearch("search?q=zelda&type=game");
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        Api.OnProblem(HttpMethod.Get, SearchPath, HttpStatusCode.BadRequest, "invalid_cursor");
        page.FindAll("button").Single(b => HasText(b, "Daha fazla")).Click();

        page.WaitForAssertion(() => page.Find(".search__more .alert--error").TextContent.Should().Contain("Liste değişti"));
        page.FindAll("button").Should().NotContain(b => HasText(b, "Tekrar dene") || HasText(b, "Daha fazla"));
        page.FindAll(".item-card").Should().HaveCount(1);
    }

    [Fact]
    public void Adding_a_hit_without_an_id_imports_it_then_saves_the_shelf_entry()
    {
        SignInAs("ninja");
        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new[] { new { @ref = "tmdb:movie:438631", id = (Guid?)null, type = "movie", title = "Dune", year = 2021, subtitle = "Denis Villeneuve", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        Api.On(HttpMethod.Post, "/api/v1/items/import", ItemJson.Movie(DuneId), HttpStatusCode.Created);
        Api.On(HttpMethod.Put, $"/api/v1/me/shelf/{DuneId}", ShelfEntry(DuneId, "movie", "planned"), HttpStatusCode.Created);
        var page = RenderSearch("search?q=dune&type=movie");

        page.WaitForAssertion(() => page.Find(".item-card__actions button").TextContent.Should().Contain("Rafa ekle"));
        page.Find("a.item-card__link").GetAttribute("href").Should().Be("items/open?ref=tmdb%3Amovie%3A438631");
        page.FindAll(".hit-hint").Should().BeEmpty();
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
    public void Adding_a_catalog_hit_reads_its_total_so_the_editor_can_cap_the_progress()
    {
        SignInAs("ninja");
        Api.AnswerAsynchronously = true;
        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new[] { new { @ref = "openLibrary:OL1W", id = MadonnaId, type = "book", title = "Kürk Mantolu Madonna", year = 1943, subtitle = "Sabahattin Ali", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        Api.On(HttpMethod.Get, $"/api/v1/items/{MadonnaId}", ItemJson.Book(MadonnaId));
        // GET /me/shelf/{id} is not registered: the fake answers 404, which means "not on the shelf".
        var page = RenderSearch("search?q=madonna&type=book");

        page.WaitForAssertion(() => page.Find(".item-card__actions button").TextContent.Should().Contain("Rafa ekle"));
        page.Find(".item-card__actions button").Click();
        page.WaitForAssertion(() => page.Find("section.dialog select"));
        page.Find("section.dialog select").Change("InProgress");

        page.WaitForAssertion(() => page.Find("section.dialog input[type=number]").GetAttribute("max").Should().Be("160"));
        page.Find("section.dialog").TextContent.Should().Contain("İlerleme (sayfa / 160)");
        Api.Requests.Should().Contain(r => r.Method == HttpMethod.Get && r.PathAndQuery == $"/api/v1/me/shelf/{MadonnaId}");
        Api.Requests.Should().NotContain(r => r.Method == HttpMethod.Post, "the hit is in the catalog already");
    }

    [Fact]
    public void An_expired_session_turns_the_cards_back_to_sign_in_links()
    {
        SignInAs("ninja");
        Api.On(HttpMethod.Get, SearchPath, new
        {
            items = new[] { new { @ref = "tmdb:movie:438631", id = (Guid?)null, type = "movie", title = "Dune", year = 2021, subtitle = "Denis Villeneuve", coverUrl = (string?)null } },
            nextCursor = (string?)null,
        });
        var page = RenderSearch("search?q=dune&type=movie");
        page.WaitForAssertion(() => page.Find(".item-card__actions button").TextContent.Should().Contain("Rafa ekle"));

        // The cookie expires while the page is open.
        Api.OnProblem(HttpMethod.Get, "/api/v1/me", HttpStatusCode.Unauthorized, "unauthorized");
        Api.OnProblem(HttpMethod.Post, "/api/v1/items/import", HttpStatusCode.Unauthorized, "unauthorized");
        page.Find(".item-card__actions button").Click();

        page.WaitForAssertion(() => page.Find(".item-card__actions a").GetAttribute("href").Should().Be("login?returnUrl=search%3Fq%3Ddune%26type%3Dmovie"));
        page.Find(".item-card__actions .alert--error").TextContent.Should().Contain("Devam etmek için giriş yap");
        page.Find("a.item-card__link").GetAttribute("href").Should().StartWith("login?returnUrl=");
        Api.Requests.Count(r => string.Equals(r.PathAndQuery, "/api/v1/me", StringComparison.Ordinal)).Should().Be(2, "the 401 reloads the session");
    }

    private static object ShelfEntry(Guid id, string type, string status) => new
    {
        item = new { id, type, title = "Dune", year = 2021, coverUrl = (string?)null, creators = new[] { "Denis Villeneuve" }, progressTotal = (int?)null },
        status,
        rating = (int?)null,
        startedOn = (string?)null,
        finishedOn = (string?)null,
        progress = (int?)null,
        review = (string?)null,
        isFavorite = false,
        createdAt = DateTimeOffset.UtcNow,
        updatedAt = DateTimeOffset.UtcNow,
    };

    private static bool HasText(AngleSharp.Dom.IElement element, string text) => element.TextContent.Contains(text, StringComparison.Ordinal);

    private List<string> SearchRequests() =>
        Api.Requests.Where(r => r.PathAndQuery.StartsWith(SearchPath, StringComparison.Ordinal)).Select(r => r.PathAndQuery).ToList();

    private IRenderedComponent<SearchPage> RenderSearch(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
        return Render<SearchPage>();
    }
}
