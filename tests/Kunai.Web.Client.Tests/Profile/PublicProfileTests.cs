using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Profile;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Kunai.Web.Client.Tests.Profile;

public sealed class PublicProfileTests : ProfileTestContext
{
    [Fact]
    public void Profile_renders_the_api_json()
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
        page.WaitForAssertion(() => page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Okudum").And.Contain("9/10"));
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
    public void A_server_error_offers_a_retry_that_loads_the_profile()
    {
        Api.AnswerAsynchronously = true;
        Api.OnProblem(HttpMethod.Get, ProfilePath, HttpStatusCode.ServiceUnavailable, "service_unavailable");
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Sunucuda bir hata oluştu"));
        Serve();
        ButtonNamed(page, "Tekrar dene").Click();

        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Kara Ninja"));
        page.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public void A_late_answer_for_the_previous_profile_never_replaces_the_new_one()
    {
        var gate = UseRequestGate();
        Serve();
        ServeProfile("other", "Başka Ninja");
        Api.On(HttpMethod.Get, "/api/v1/users/other/shelf", ShelfPage("Hyperion", nextCursor: null));
        gate.Hold(ProfilePath);
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForElement(".spinner");

        // "Profilim" (or any profile link) while ninja's profile is still loading: the page instance is reused.
        page.Render(p => p.Add(x => x.Username, "other"));
        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Başka Ninja"));
        var renders = page.RenderCount;
        gate.Release(ProfilePath);

        // Ninja's answer arrives last; the page renders once more when that load ends, and must ignore it.
        page.WaitForAssertion(() => QueriesTo(ProfilePath).Should().ContainSingle());
        page.WaitForState(() => page.RenderCount > renders);
        page.Find("h1").TextContent.Should().Be("Başka Ninja");
        page.Find(".profile-header").TextContent.Should().Contain("@other");
        page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Hyperion");
    }

    [Fact]
    public void Another_username_loads_that_profile_and_starts_again_on_the_shelf_tab()
    {
        Serve();
        Api.On(HttpMethod.Get, $"{ProfilePath}/stats", Stats(2026, 12, 2026));
        ServeProfile("other", "Başka Ninja");
        Api.On(HttpMethod.Get, "/api/v1/users/other/shelf", ShelfPage("Hyperion", nextCursor: null));
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Kara Ninja"));
        ButtonNamed(page, "İstatistikler").Click();
        page.WaitForElement("#panel-Stats .month-bars");

        page.Render(p => p.Add(x => x.Username, "other"));

        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Başka Ninja"));
        page.Find("#tab-Shelf").GetAttribute("aria-selected").Should().Be("true");
        page.Find("#tab-Stats").GetAttribute("aria-selected").Should().Be("false");
        page.Find("#panel-Shelf").HasAttribute("hidden").Should().BeFalse();
        page.Find("#panel-Stats").HasAttribute("hidden").Should().BeTrue();
        page.Find("#panel-Stats").Children.Should().BeEmpty("the new profile's stats load only when their tab is opened");
        page.WaitForAssertion(() => page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Hyperion"));
        QueriesTo("/api/v1/users/other/shelf").Should().ContainSingle();
    }

    [Fact]
    public void A_panel_mounts_when_its_tab_is_first_opened_and_keeps_its_content()
    {
        Serve();
        Api.On(HttpMethod.Get, $"{ProfilePath}/stats", Stats(2026, 12, 2026));
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Dune"));
        page.Find("#panel-Stats").Children.Should().BeEmpty("nothing loads for a tab that was never opened");

        ButtonNamed(page, "İstatistikler").Click();
        page.WaitForElement("#panel-Stats .month-bars");
        ButtonNamed(page, "Raf").Click();
        ButtonNamed(page, "İstatistikler").Click();

        page.Find("#panel-Shelf").HasAttribute("hidden").Should().BeTrue();
        page.FindAll("#panel-Shelf .item-card").Should().ContainSingle("a hidden panel keeps its content");
        QueriesTo($"{ProfilePath}/shelf").Should().ContainSingle();
        QueriesTo($"{ProfilePath}/stats").Should().ContainSingle("coming back does not load the tab again");
    }

    [Fact]
    public void Tabs_follow_the_aria_tabs_pattern()
    {
        Serve();
        Api.On(HttpMethod.Get, $"{ProfilePath}/stats", Stats(2026, 12, 2026));
        Api.On(HttpMethod.Get, $"{ProfilePath}/goals/2026", Goals(2026));
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForElement("[role=tablist]");
        // bUnit prints a button's element reference only in the render that created it, so note them now.
        var references = page.FindAll("[role=tab]").ToDictionary(t => t.Id!, t => t.GetAttribute("blazor:elementreference"), StringComparer.Ordinal);
        references.Values.Should().AllSatisfy(r => r.Should().NotBeNullOrEmpty());

        page.Find("[role=tablist]").GetAttribute("aria-labelledby").Should().Be("profile-tabs-title");
        page.FindAll("[role=tab]").Select(t => t.GetAttribute("aria-controls"))
            .Should().AllSatisfy(id => page.FindAll($"#{id}[role=tabpanel]").Should().ContainSingle("aria-controls must name a panel that exists"));
        page.FindAll("[role=tabpanel]").Select(p => p.GetAttribute("aria-labelledby")).Should().Equal("tab-Shelf", "tab-Stats", "tab-Goals");
        ShouldBeSelected("tab-Shelf");

        Press("ArrowRight");
        ShouldBeSelected("tab-Stats");
        ShouldHaveFocused(1, "tab-Stats");

        Press("End");
        ShouldBeSelected("tab-Goals");
        ShouldHaveFocused(2, "tab-Goals");

        Press("ArrowRight");
        ShouldBeSelected("tab-Shelf");
        ShouldHaveFocused(3, "tab-Shelf");

        Press("ArrowLeft");
        ShouldBeSelected("tab-Goals");
        ShouldHaveFocused(4, "tab-Goals");

        Press("Home");
        ShouldBeSelected("tab-Shelf");
        ShouldHaveFocused(5, "tab-Shelf");

        Press("Enter");
        ShouldBeSelected("tab-Shelf");
        JSInterop.VerifyFocusAsyncInvoke(5, "other keys keep their usual meaning");

        void Press(string key) => page.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = key });

        void ShouldBeSelected(string id)
        {
            var tabs = page.FindAll("[role=tab]");
            tabs.Select(t => t.GetAttribute("aria-selected")).Should().Equal(tabs.Select(t => Is(t, "true", "false")));
            tabs.Select(t => t.GetAttribute("tabindex")).Should().Equal(tabs.Select(t => Is(t, "0", "-1")), "only the selected tab is in the Tab order");
            page.FindAll("[role=tabpanel]:not([hidden])").Single().GetAttribute("aria-labelledby").Should().Be(id);

            string Is(AngleSharp.Dom.IElement tab, string selected, string other) => string.Equals(tab.Id, id, StringComparison.Ordinal) ? selected : other;
        }

        void ShouldHaveFocused(int calls, string id) =>
            JSInterop.VerifyFocusAsyncInvoke(calls)[^1].Arguments[0].Should().BeOfType<ElementReference>()
                .Which.Id.Should().Be(references[id], $"focus moves to {id}");
    }
}
