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

        // Ninja's answer arrives last (HttpClient turns it into a cancellation, since the token was cancelled); the page
        // renders once more when that load ends, and must ignore it.
        page.WaitForAssertion(() => QueriesTo(ProfilePath).Should().ContainSingle());
        page.WaitForState(() => page.RenderCount > renders);
        page.Find("h1").TextContent.Should().Be("Başka Ninja");
        page.Find(".profile-header").TextContent.Should().Contain("@other");
        page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Hyperion");
        page.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public void A_cancelled_load_for_the_previous_profile_never_hides_the_new_one()
    {
        var gate = UseRequestGate();
        Serve();
        ServeProfile("other", "Başka Ninja");
        Api.On(HttpMethod.Get, "/api/v1/users/other/shelf", ShelfPage("Hyperion", nextCursor: null));
        // The browser aborts the fetch: ninja's request ends with a cancellation instead of a late answer.
        gate.Hold(ProfilePath, honourCancellation: true);
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForElement(".spinner");
        var renders = page.RenderCount;

        page.Render(p => p.Add(x => x.Username, "other"));

        // Three renders: the new parameters, the end of the cancelled load and the end of the new one.
        page.WaitForState(() => page.RenderCount >= renders + 3);
        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Başka Ninja"));
        page.FindAll(".alert--error").Should().BeEmpty("the cancelled load is not a network error");
        page.Find(".profile-header").TextContent.Should().Contain("@other");
        QueriesTo(ProfilePath).Should().BeEmpty("ninja's request never left");
    }

    [Fact]
    public void A_profile_that_answered_before_the_switch_never_replaces_the_new_page()
    {
        var gate = UseRequestGate();
        Serve();
        // The session is still loading: ninja's profile has answered, and its load waits for the viewer.
        gate.Hold("/api/v1/me");
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => QueriesTo(ProfilePath).Should().ContainSingle());

        // A profile that does not exist answers at once, while ninja's load is still waiting.
        page.Render(p => p.Add(x => x.Username, "ghost"));
        page.WaitForAssertion(() => page.Find(".empty-state").TextContent.Should().Contain("Bu kullanıcı yok"));
        var renders = page.RenderCount;
        gate.Release("/api/v1/me");

        // Ninja's load ends now, with a profile in hand: it must not show it on ghost's page.
        page.WaitForState(() => page.RenderCount > renders);
        page.Find(".empty-state").TextContent.Should().Contain("Bu kullanıcı yok");
        page.FindAll(".profile-header").Should().BeEmpty();
    }

    [Fact]
    public void Retry_keeps_the_focus_on_the_page_heading()
    {
        Api.AnswerAsynchronously = true;
        Api.OnProblem(HttpMethod.Get, ProfilePath, HttpStatusCode.ServiceUnavailable, "service_unavailable");
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find(".alert--error"));
        var heading = page.Find("h1").GetAttribute("blazor:elementreference");
        Serve();

        ButtonNamed(page, "Tekrar dene").Click();

        // "Tekrar dene" goes away with the problem: the heading above it, which stays while loading, takes its focus,
        // and then the profile's own heading once it is there.
        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Kara Ninja"));
        var profileHeading = page.Find("h1").GetAttribute("blazor:elementreference");
        profileHeading.Should().NotBeNullOrEmpty();
        page.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke(2).Select(i => i.Arguments[0]).OfType<ElementReference>().Select(r => r.Id)
            .Should().Equal(heading, profileHeading));
    }

    [Fact]
    public void Tab_keys_do_not_scroll_the_page()
    {
        Serve();
        ServeProfile("other", "Başka Ninja");
        Api.On(HttpMethod.Get, "/api/v1/users/other/shelf", ShelfPage("Hyperion", nextCursor: null));
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForElement("[role=tablist]");
        var tablist = page.Find("[role=tablist]").GetAttribute("blazor:elementreference");

        // Home and End would also scroll the page; the module cancels that for the tab keys alone (bUnit has no
        // default actions, so this checks that the tab list is handed to it).
        page.WaitForAssertion(() => GuardedTablist(1).Should().Be(tablist));

        page.Render(p => p.Add(x => x.Username, "other"));
        page.WaitForAssertion(() => page.Find("h1").TextContent.Should().Be("Başka Ninja"));
        var otherTablist = page.Find("[role=tablist]").GetAttribute("blazor:elementreference");
        otherTablist.Should().NotBe(tablist, "a new profile renders a new tab list");
        page.WaitForAssertion(() => GuardedTablist(2).Should().Be(otherTablist, "the new tab list gets the listener too"));

        string? GuardedTablist(int calls) =>
            TabKeys.VerifyInvoke("preventTabKeyScrolling", calls)[^1].Arguments[0].Should().BeOfType<ElementReference>().Which.Id;
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

    [Fact]
    public void A_tab_key_with_a_modifier_is_left_to_the_browser()
    {
        Serve();
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForElement("[role=tablist]");

        // Alt+ArrowLeft goes back, Ctrl+End scrolls to the bottom: the browser's, like PublicProfile.razor.js leaves them.
        page.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft", AltKey = true });
        page.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "End", CtrlKey = true });
        page.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "Home", MetaKey = true });
        page.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        page.Find("#tab-Shelf").GetAttribute("aria-selected").Should().Be("true");
        JSInterop.Invocations.Select(i => i.Identifier).Should().NotContain(i => i.Contains("focus", StringComparison.OrdinalIgnoreCase));
    }

    private string? FocusedReference(int calls) =>
        JSInterop.VerifyFocusAsyncInvoke(calls)[^1].Arguments[0].Should().BeOfType<ElementReference>().Which.Id;
}
