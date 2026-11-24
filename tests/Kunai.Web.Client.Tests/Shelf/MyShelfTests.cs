using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Shelf;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Shelf;

public sealed class MyShelfTests : KunaiTestContext
{
    private const string ShelfPath = "/api/v1/me/shelf";
    private static readonly Guid DuneId = Guid.Parse("0b6f5a9e-2a43-4d6c-9a43-6a2f0b1c7d01");
    private static readonly Guid HadesId = Guid.Parse("5c1e7f20-8d3b-4b0e-b7a5-2f9c4e6d8a02");

    [Fact]
    public void Shows_the_entries_with_their_status_rating_progress_and_the_total()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));

        var page = Render<MyShelf>();

        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));
        page.Find(".shelf-header__count").TextContent.Should().Be("2 içerik");
        var dune = page.FindAll(".item-card")[0];
        dune.QuerySelector(".item-card__title")!.TextContent.Should().Be("Dune");
        dune.QuerySelector("a")!.GetAttribute("href").Should().Be($"items/{DuneId}");
        dune.QuerySelector(".shelf-card__rating")!.TextContent.Should().EndWith("★ 8");
        dune.QuerySelector(".shelf-card__favorite").Should().NotBeNull();
        page.WaitForAssertion(() => page.FindAll(".item-card")[0].QuerySelector(".shelf-card__progress")!.TextContent.Should().EndWith("120 / 412 sayfa"));
        page.WaitForAssertion(() => page.FindAll(".status-badge").Select(b => b.TextContent).Should().Equal("Okuyorum", "Bitirdim"));
        page.FindAll(".item-card")[1].QuerySelector(".shelf-card__progress")!.TextContent.Should().EndWith("%100");
    }

    [Fact]
    public void Picking_a_status_keeps_the_filters_in_the_url_and_reloads_with_them()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 1, Dune()));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("shelf?type=book&favorite=true&sort=-rating");
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        page.FindAll(".chips button").Single(b => HasText(b, "Okuyorum")).Click();

        navigation.Uri.Should().Contain("type=book").And.Contain("favorite=true").And.Contain("sort=-rating").And.Contain("status=inProgress");
        page.WaitForAssertion(() => ShelfRequests().Should().HaveCount(2));
        ShelfRequests()[1].Should().Contain("Type=book").And.Contain("Status=inProgress").And.Contain("Favorite=true")
            .And.Contain("Sort=-rating").And.Contain("Limit=24");
        page.Find(".chips button[aria-pressed=true]").TextContent.Should().Be("Okuyorum");
    }

    [Fact]
    public void More_appends_the_next_page_using_the_cursor()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page("page-2", 2, Dune()));
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));

        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Hades()));
        page.FindAll("button").Single(b => HasText(b, "Daha fazla")).Click();

        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));
        ShelfRequests()[1].Should().Contain("Cursor=page-2");
        page.FindAll("button").Should().NotContain(b => HasText(b, "Daha fazla"));
    }

    [Fact]
    public void Removing_an_entry_in_the_editor_drops_its_card_and_the_count()
    {
        Api.On(HttpMethod.Get, ShelfPath, Page(null, 2, Dune(), Hades()));
        Api.On(HttpMethod.Delete, $"{ShelfPath}/{DuneId}", null, HttpStatusCode.NoContent);
        var page = Render<MyShelf>();
        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(2));

        page.FindAll(".shelf-card__edit")[0].Click();
        page.WaitForAssertion(() => page.FindAll(".dialog button").Should().Contain(b => HasText(b, "Raftan kaldır")));
        page.FindAll(".dialog button").Single(b => HasText(b, "Raftan kaldır")).Click();

        page.WaitForAssertion(() => page.FindAll(".item-card").Should().HaveCount(1));
        page.Find(".shelf-header__count").TextContent.Should().Be("1 içerik");
        page.Find(".alert--success").TextContent.Should().Be("Dune raftan kaldırıldı.");
        page.FindAll(".dialog").Should().BeEmpty();
        Api.Requests.Should().Contain(r => r.Method == HttpMethod.Delete && r.PathAndQuery == $"{ShelfPath}/{DuneId}");
    }

    [Fact]
    public void An_api_error_shows_the_turkish_message_and_a_retry()
    {
        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.TooManyRequests, "rate_limited");

        var page = Render<MyShelf>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Çok fazla deneme yaptın"));
        page.FindAll("button").Should().Contain(b => HasText(b, "Tekrar dene"));
        page.FindAll(".item-card").Should().BeEmpty();
    }

    private static bool HasText(AngleSharp.Dom.IElement element, string text) => string.Equals(element.TextContent, text, StringComparison.Ordinal);

    private List<string> ShelfRequests() =>
        [.. Api.Requests.Where(r => r.Method == HttpMethod.Get && r.PathAndQuery.StartsWith(ShelfPath, StringComparison.Ordinal)).Select(r => r.PathAndQuery)];

    private static object Page(string? nextCursor, int totalCount, params object[] items) => new { items, nextCursor, totalCount };

    private static object Dune() => Entry(DuneId, "book", "Dune", "inProgress", rating: 8, progress: 120, progressTotal: 412, favorite: true);

    private static object Hades() => Entry(HadesId, "game", "Hades", "completed", rating: null, progress: 100, progressTotal: null, favorite: false);

    // The JSON the API sends: camelCase names, enum strings, dates as yyyy-MM-dd.
    private static object Entry(Guid id, string type, string title, string status, int? rating, int? progress, int? progressTotal, bool favorite) => new
    {
        item = new { id, type, title, year = 2020, coverUrl = (string?)null, creators = new[] { "Yazar" }, progressTotal },
        status,
        rating,
        startedOn = "2026-11-01",
        finishedOn = string.Equals(status, "completed", StringComparison.Ordinal) ? "2026-11-20" : null,
        progress,
        review = (string?)null,
        isFavorite = favorite,
        createdAt = DateTimeOffset.Parse("2026-11-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        updatedAt = DateTimeOffset.Parse("2026-11-20T18:30:00Z", System.Globalization.CultureInfo.InvariantCulture),
    };
}
