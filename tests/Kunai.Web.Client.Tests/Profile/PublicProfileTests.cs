using System.Net;

using AngleSharp.Dom;

using Bunit;

using Kunai.Web.Client.Pages.Profile;
using Kunai.Web.Client.Tests.Infrastructure;

namespace Kunai.Web.Client.Tests.Profile;

public sealed class PublicProfileTests : KunaiTestContext
{
    private const string ProfilePath = "/api/v1/users/ninja";

    [Fact]
    public void Profile_renders_the_api_json_and_pages_the_read_only_shelf()
    {
        Serve();
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));

        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Kara Ninja"));
        var header = page.Find(".profile-header").TextContent;
        header.Should().Contain("@ninja").And.Contain("Katılım: 15 Mart 2025").And.Contain("Yeşil kuşak")
            .And.Contain("120 / 150 puan").And.Contain("sonraki kuşağa 30 puan kaldı");
        page.FindAll("a[href=settings]").Should().BeEmpty("only the owner may edit the profile");
        page.FindAll(".stat-grid .stat__value").Select(v => v.TextContent).Should().Equal("42", "7", "15");
        page.Find(".stat-grid").TextContent.Should().Contain("Kitap").And.Contain("Oyun").And.Contain("2026 yılında");
        page.Find("#profile-favorites-title").ParentElement!.TextContent.Should().Contain("Dune").And.Contain("Frank Herbert");

        page.WaitForAssertion(() => page.Find("[role=tabpanel] .item-card").TextContent.Should().Contain("Okudum").And.Contain("9/10"));
        Api.On(HttpMethod.Get, $"{ProfilePath}/shelf", ShelfPage("Hyperion", nextCursor: null));
        ButtonNamed(page, "Daha fazla").Click();

        page.WaitForAssertion(() => page.FindAll("[role=tabpanel] .item-card").Should().HaveCount(2));
        ButtonNamed(page, "Kitap").Click();
        page.WaitForAssertion(() => ShelfQueries().Last().Should().Contain("Type=book"));
        ShelfQueries().Should().HaveCount(3);
        ShelfQueries().ElementAt(1).Should().Contain("Cursor=next-page");
    }

    [Fact]
    public void Missing_or_private_user_shows_the_friendly_message()
    {
        Api.OnProblem(HttpMethod.Get, "/api/v1/users/ghost", HttpStatusCode.NotFound, "user_not_found");

        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ghost"));

        page.WaitForAssertion(() => page.Find(".empty-state").TextContent.Should().Contain("Bu kullanıcı yok ya da profili gizli"));
        page.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public void Stats_tab_draws_twelve_months_and_asks_for_the_chosen_year()
    {
        Serve();
        Api.On(HttpMethod.Get, $"{ProfilePath}/stats", new
        {
            year = 2026,
            availableYears = new[] { 2026, 2025 },
            types = new object[]
            {
                new { type = "book", completed = 12, averageRating = 8.4 },
                new { type = "game", completed = 3, averageRating = (double?)null },
            },
            completedByMonth = new[] { 1, 2, 0, 3, 1, 0, 2, 1, 0, 1, 1, 3 },
            pagesRead = 12450,
            episodesWatched = 86,
            chaptersRead = 0,
        });
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));

        page.WaitForElement("[role=tab]");
        ButtonNamed(page, "İstatistikler").Click();

        page.WaitForAssertion(() => page.FindAll(".month-bars__bar").Should().HaveCount(12));
        page.FindAll(".month-bars__labels span").Select(s => s.TextContent).Should().Equal("O", "Ş", "M", "N", "M", "H", "T", "A", "E", "E", "K", "A");
        page.Find(".month-bars").GetAttribute("aria-label").Should().Contain("Aralık 3");
        var panel = page.Find("#panel-Stats").TextContent;
        panel.Should().Contain("Ortalama puan 8,4").And.Contain("Puan verilmemiş").And.Contain("12.450").And.Contain("Okunan sayfa");

        page.Find("#stats-year").Change("2025");

        page.WaitForAssertion(() => Api.Requests.Select(r => r.PathAndQuery).Should().Contain($"{ProfilePath}/stats?year=2025"));
    }

    [Fact]
    public void Owner_saves_goals_and_leaves_out_the_empty_types()
    {
        SignInAs("ninja");
        Serve();
        Api.On(HttpMethod.Get, $"{ProfilePath}/goals/2026", Goals(("book", 24, 12, 50)));
        Api.On(HttpMethod.Put, "/api/v1/me/goals/2026", Goals(("book", 30, 12, 40), ("game", 5, 3, 60)));
        var page = OpenGoalsEditor();

        page.Find("#goal-book").GetAttribute("value").Should().Be("24");
        page.Find("#goal-book").Change("30");
        page.Find("#goal-game").Change("5");
        page.Find("form.goals-form").Submit();

        page.WaitForAssertion(() => page.Find(".alert--success").TextContent.Should().Contain("Görevler kaydedildi"));
        var body = Api.BodiesSentTo(HttpMethod.Put, "/api/v1/me/goals/2026").Single();
        body.Should().Contain("\"type\":\"book\"").And.Contain("\"target\":30").And.Contain("\"type\":\"game\"").And.Contain("\"target\":5");
        body.Should().NotContain("movie").And.NotContain("anime");
        page.FindAll(".goal").Should().HaveCount(2);
    }

    [Fact]
    public void Rejected_goals_show_the_turkish_message()
    {
        SignInAs("ninja");
        Serve();
        Api.On(HttpMethod.Get, $"{ProfilePath}/goals/2026", Goals());
        Api.OnProblem(HttpMethod.Put, "/api/v1/me/goals/2026", HttpStatusCode.BadRequest, "validation_failed",
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["targets[0].target"] = ["target_out_of_range"] });
        var page = OpenGoalsEditor();

        page.Find("#goal-book").Change("10");
        page.Find("form.goals-form").Submit();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent
            .Should().Contain("Bazı alanları düzeltmen gerekiyor.").And.Contain("Hedef 1 ile 10000 arasında olmalı."));
        page.FindAll("form.goals-form").Should().ContainSingle("the form stays open to fix the target");
    }

    private IRenderedComponent<PublicProfile> OpenGoalsEditor()
    {
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("a[href=settings]").TextContent.Should().Be("Profili düzenle"));
        ButtonNamed(page, "Görevler").Click();
        page.WaitForAssertion(() => ButtonNamed(page, "Görevleri düzenle"));
        ButtonNamed(page, "Görevleri düzenle").Click();
        page.WaitForElement("form.goals-form");
        return page;
    }

    private static IElement ButtonNamed(IRenderedComponent<PublicProfile> page, string text) =>
        page.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    private IEnumerable<string> ShelfQueries() =>
        Api.Requests.Select(r => r.PathAndQuery).Where(p => p.StartsWith($"{ProfilePath}/shelf", StringComparison.Ordinal));

    private void Serve()
    {
        Api.On(HttpMethod.Get, ProfilePath, new
        {
            username = "ninja",
            displayName = "Kara Ninja",
            bio = "Gece okur, gündüz oynar.",
            avatarUrl = "/api/v1/avatars/ninja-07.svg?belt=green",
            joinedAt = "2025-03-15T12:00:00Z",
            rank = new { code = "green", label = new { tr = "Yeşil kuşak", en = "Green belt" }, points = 120, nextCode = "blue", nextAt = 150 },
            counts = new[]
            {
                new { type = "book", planned = 4, inProgress = 1, completed = 42, paused = 0, dropped = 2 },
                new { type = "movie", planned = 3, inProgress = 0, completed = 0, paused = 0, dropped = 0 },
                new { type = "game", planned = 1, inProgress = 2, completed = 7, paused = 0, dropped = 0 },
            },
            thisYear = new { year = 2026, completed = new[] { new { type = "book", count = 12 }, new { type = "game", count = 3 } } },
            favorites = new[] { Item("Dune") },
        });
        Api.On(HttpMethod.Get, $"{ProfilePath}/shelf", ShelfPage("Dune", nextCursor: "next-page"));
        Api.On(HttpMethod.Get, "/api/v1/ranks", new object[]
        {
            new { code = "white", label = new { tr = "Beyaz kuşak", en = "White belt" }, minPoints = 0, color = "#f4f4f0" },
            new { code = "green", label = new { tr = "Yeşil kuşak", en = "Green belt" }, minPoints = 100, color = "#2e9e4f" },
        });
    }

    private static object Item(string title) => new
    {
        id = Guid.NewGuid(),
        type = "book",
        title,
        year = 1965,
        coverUrl = (string?)null,
        creators = new[] { "Frank Herbert" },
        progressTotal = 412,
    };

    private static object ShelfPage(string title, string? nextCursor) => new
    {
        items = new[]
        {
            new
            {
                item = Item(title),
                status = "completed",
                rating = 9,
                startedOn = "2026-01-02",
                finishedOn = "2026-02-01",
                progress = 412,
                review = (string?)null,
                isFavorite = true,
                createdAt = "2026-01-02T10:00:00Z",
                updatedAt = "2026-02-01T10:00:00Z",
            },
        },
        nextCursor,
        totalCount = 2,
    };

    private static object Goals(params (string Type, int Target, int Completed, int Percent)[] goals) => new
    {
        year = 2026,
        goals = goals.Select(g => new { type = g.Type, target = g.Target, completed = g.Completed, percent = g.Percent }).ToArray(),
    };
}
