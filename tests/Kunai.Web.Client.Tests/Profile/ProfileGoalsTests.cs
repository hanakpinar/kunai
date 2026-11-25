using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Profile;

using Microsoft.AspNetCore.Components;

namespace Kunai.Web.Client.Tests.Profile;

public sealed class ProfileGoalsTests : ProfileTestContext
{
    private const string GoalsPath = $"{ProfilePath}/goals";

    [Fact]
    public void Owner_saves_goals_and_leaves_out_the_empty_types()
    {
        SignInAs("ninja");
        Serve();
        Api.On(HttpMethod.Get, $"{GoalsPath}/2026", Goals(2026, ("book", 24, 12, 50)));
        Api.On(HttpMethod.Put, "/api/v1/me/goals/2026", Goals(2026, ("book", 30, 12, 40), ("game", 5, 3, 60)));
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
        Api.On(HttpMethod.Get, $"{GoalsPath}/2026", Goals(2026));
        Api.OnProblem(HttpMethod.Put, "/api/v1/me/goals/2026", HttpStatusCode.BadRequest, "validation_failed",
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["targets[0].target"] = ["target_out_of_range"] });
        var page = OpenGoalsEditor();

        page.Find("#goal-book").Change("10");
        page.Find("form.goals-form").Submit();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent
            .Should().Contain("Bazı alanları düzeltmen gerekiyor.").And.Contain("Hedef 1 ile 10000 arasında olmalı."));
        page.FindAll("form.goals-form").Should().ContainSingle("the form stays open to fix the target");
    }

    [Fact]
    public void A_target_that_is_not_a_whole_number_is_explained_in_turkish()
    {
        SignInAs("ninja");
        Serve();
        Api.On(HttpMethod.Get, $"{GoalsPath}/2026", Goals(2026));
        var page = OpenGoalsEditor();
        var book = page.Find("#goal-book");
        book.GetAttribute("step").Should().Be("1");
        book.GetAttribute("inputmode").Should().Be("numeric");

        page.Find("#goal-book").Change("1.5");
        page.Find("form.goals-form").Submit();

        page.WaitForAssertion(() => page.Find(".validation-message").TextContent.Should().Be("Kitap hedefi bir tam sayı olmalı."));
        Api.BodiesSentTo(HttpMethod.Put, "/api/v1/me/goals/2026").Should().BeEmpty();
    }

    [Fact]
    public void The_editor_speaks_of_the_year_being_edited()
    {
        SignInAs("ninja");
        Serve();
        Api.On(HttpMethod.Get, $"{GoalsPath}/2026", Goals(2026));
        Api.On(HttpMethod.Get, $"{GoalsPath}/2027", Goals(2027, ("book", 12, 0, 0)));
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("a[href=settings]"));
        ButtonNamed(page, "Görevler").Click();
        page.WaitForElement("#goals-year");

        page.Find("#goals-year").Change("2027");
        page.WaitForAssertion(() => page.Find(".goal").TextContent.Should().Contain("Kitap"));
        ButtonNamed(page, "Görevleri düzenle").Click();

        var form = page.WaitForElement("form.goals-form");
        form.QuerySelector("h3")!.TextContent.Should().Be("2027 görevlerin");
        form.QuerySelector("p")!.TextContent.Should().StartWith("2027 yılında her türden kaç tane bitirmek istiyorsun?");
    }

    [Fact]
    public void A_failed_load_offers_a_retry_below_the_alert()
    {
        Api.AnswerAsynchronously = true;
        Api.OnProblem(HttpMethod.Get, $"{GoalsPath}/2026", HttpStatusCode.ServiceUnavailable, "service_unavailable");
        var panelFocused = 0;
        var page = Render<ProfileGoals>(p => p
            .Add(x => x.Username, "ninja")
            .Add(x => x.Year, 2026)
            .AddCascadingValue(ProfilePanel.FocusName, (Func<ValueTask>)(() =>
            {
                panelFocused++;
                return ValueTask.CompletedTask;
            })));

        // The alert sits in a wrapper this folder styles (ProblemAlert's own markup is out of reach of scoped CSS).
        page.WaitForAssertion(() => page.Find(".load-problem > .alert--error").TextContent.Should().Contain("Sunucuda bir hata oluştu"));
        Api.On(HttpMethod.Get, $"{GoalsPath}/2026", Goals(2026, ("book", 24, 12, 50)));
        page.Find(".load-problem > button").Click();

        page.WaitForAssertion(() => page.FindAll(".goal").Should().ContainSingle());
        page.FindAll(".load-problem").Should().BeEmpty();
        panelFocused.Should().Be(1, "the focused button went away, so the tab panel takes the focus");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_editor_takes_the_focus_and_gives_it_back_when_it_closes(bool save)
    {
        SignInAs("ninja");
        Serve();
        Api.On(HttpMethod.Get, $"{GoalsPath}/2026", Goals(2026, ("book", 24, 12, 50)));
        Api.On(HttpMethod.Put, "/api/v1/me/goals/2026", Goals(2026, ("book", 30, 12, 40)));
        var page = OpenGoalsEditor();

        // "Görevleri düzenle" is gone: the editor's heading, which names the year, takes the focus.
        var heading = page.Find("form.goals-form h3");
        heading.GetAttribute("tabindex").Should().Be("-1");
        page.WaitForAssertion(() => FocusedReference(1).Should().Be(heading.GetAttribute("blazor:elementreference")));

        if (save)
        {
            page.Find("#goal-book").Change("30");
            page.Find("form.goals-form").Submit();
            page.WaitForAssertion(() => page.Find(".alert--success"));
        }
        else
        {
            ButtonNamed(page, "Vazgeç").Click();
        }

        // Its buttons are gone too: back on "Görevleri düzenle", where the keyboard user started.
        page.WaitForAssertion(() => FocusedReference(2).Should().Be(ButtonNamed(page, "Görevleri düzenle").GetAttribute("blazor:elementreference")));
        page.FindAll("form.goals-form").Should().BeEmpty();
    }

    private string? FocusedReference(int calls) =>
        JSInterop.VerifyFocusAsyncInvoke(calls)[^1].Arguments[0].Should().BeOfType<ElementReference>().Which.Id;

    [Fact]
    public void An_ended_session_on_save_signs_the_page_out_and_links_to_the_sign_in()
    {
        SignInAs("ninja");
        Serve();
        Api.On(HttpMethod.Get, $"{GoalsPath}/2026", Goals(2026));
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("a[href=settings]"));
        ButtonNamed(page, "Görevler").Click();
        // bUnit prints an element's reference only in the render that created it: note the year select's now.
        var year = page.Find("#goals-year").GetAttribute("blazor:elementreference");
        year.Should().NotBeNullOrEmpty();
        page.WaitForAssertion(() => ButtonNamed(page, "Görevleri düzenle"));
        ButtonNamed(page, "Görevleri düzenle").Click();
        page.WaitForElement("form.goals-form");

        Api.OnProblem(HttpMethod.Get, "/api/v1/me", HttpStatusCode.Unauthorized, "unauthorized");
        Api.OnProblem(HttpMethod.Put, "/api/v1/me/goals/2026", HttpStatusCode.Unauthorized, "unauthorized");
        page.Find("#goal-book").Change("10");
        page.Find("form.goals-form").Submit();

        page.WaitForAssertion(() => page.Find("form.goals-form .alert--error").TextContent.Should().Contain("Devam etmek için giriş yap."));
        page.Find("form.goals-form a").GetAttribute("href").Should().StartWith("login?returnUrl=");
        HasButton(page, "Tekrar dene").Should().BeFalse();
        page.WaitForAssertion(() => page.FindAll("a[href=settings]").Should().BeEmpty("the session is gone, and with it the owner's controls"));
        page.Find("#panel-Goals").TextContent.Should().Contain("Kendi görevlerini koymak için");
        QueriesTo("/api/v1/me").Should().HaveCountGreaterThan(1, "the session was asked again");

        // No "Görevleri düzenle" to go back to any more: the year select, enabled again, takes the focus.
        ButtonNamed(page, "Vazgeç").Click();
        page.WaitForAssertion(() => FocusedReference(2).Should().Be(year));
    }
}
