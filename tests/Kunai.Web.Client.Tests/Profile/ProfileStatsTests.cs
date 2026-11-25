using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Profile;

namespace Kunai.Web.Client.Tests.Profile;

public sealed class ProfileStatsTests : ProfileTestContext
{
    private const string StatsPath = $"{ProfilePath}/stats";

    [Fact]
    public void Stats_tab_draws_twelve_months_and_asks_for_the_chosen_year()
    {
        Serve();
        Api.On(HttpMethod.Get, StatsPath, Stats(2026, 12, 2026, 2025));
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));

        page.WaitForElement("[role=tab]");
        ButtonNamed(page, "İstatistikler").Click();

        page.WaitForAssertion(() => page.FindAll(".month-bars__bar").Should().HaveCount(12));
        page.FindAll(".month-bars__labels span").Select(s => s.TextContent).Should().Equal("O", "Ş", "M", "N", "M", "H", "T", "A", "E", "E", "K", "A");
        page.Find(".month-bars").GetAttribute("aria-label").Should().Contain("Aralık 3");
        var panel = page.Find("#panel-Stats").TextContent;
        panel.Should().Contain("Ortalama puan 8,4").And.Contain("Puan verilmemiş").And.Contain("12.450").And.Contain("Okunan sayfa");

        page.Find("#stats-year").Change("2025");

        page.WaitForAssertion(() => QueriesTo(StatsPath).Should().Contain($"{StatsPath}?year=2025"));
    }

    [Fact]
    public void A_failed_year_change_puts_the_select_back_on_the_year_on_screen()
    {
        var gate = UseRequestGate();
        Api.On(HttpMethod.Get, StatsPath, Stats(2026, 12, 2026, 2025));
        var page = Render<ProfileStats>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => SelectedYear(page).Should().Be("2026"));

        gate.Hold(StatsPath);
        Api.OnProblem(HttpMethod.Get, StatsPath, HttpStatusCode.ServiceUnavailable, "service_unavailable");
        page.Find("#stats-year").Change("2025");

        // While 2025 loads the select shows it, so the failure below renders a different value and moves it back.
        page.WaitForAssertion(() => SelectedYear(page).Should().Be("2025"));
        page.Find("#stats-year").HasAttribute("disabled").Should().BeTrue();
        gate.Release(StatsPath);

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Sunucuda bir hata oluştu"));
        SelectedYear(page).Should().Be("2026", "the cards still show 2026");
        page.Find(".stat__value").TextContent.Should().Be("12");

        Api.On(HttpMethod.Get, StatsPath, Stats(2025, 4, 2026, 2025));
        ButtonNamed(page, "Tekrar dene").Click();

        page.WaitForAssertion(() => page.Find(".stat__value").TextContent.Should().Be("4"));
        SelectedYear(page).Should().Be("2025");
        page.FindAll(".alert--error").Should().BeEmpty();
        QueriesTo(StatsPath).Should().Equal(StatsPath, $"{StatsPath}?year=2025", $"{StatsPath}?year=2025");
    }

    [Fact]
    public void A_failed_first_load_offers_a_retry()
    {
        Api.AnswerAsynchronously = true;
        Api.OnProblem(HttpMethod.Get, StatsPath, HttpStatusCode.InternalServerError, "internal_error");
        var panelFocused = 0;
        var page = Render<ProfileStats>(p => p
            .Add(x => x.Username, "ninja")
            .AddCascadingValue(ProfilePanel.FocusName, (Func<ValueTask>)(() =>
            {
                panelFocused++;
                return ValueTask.CompletedTask;
            })));

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Sunucuda bir hata oluştu"));
        page.FindAll("#stats-year").Should().BeEmpty("there is no year on screen yet");
        Api.On(HttpMethod.Get, StatsPath, Stats(2026, 12, 2026));
        ButtonNamed(page, "Tekrar dene").Click();

        // The button had the focus and goes away with the problem: the tab panel around the stats takes it.
        page.WaitForAssertion(() => panelFocused.Should().Be(1));
        page.WaitForAssertion(() => page.FindAll(".month-bars__bar").Should().HaveCount(12));
        SelectedYear(page).Should().Be("2026");
        QueriesTo(StatsPath).Should().Equal(StatsPath, StatsPath);
        panelFocused.Should().Be(1, "only the retry moves the focus");
    }

    [Fact]
    public void A_rejected_year_does_not_offer_a_retry()
    {
        Api.OnProblem(HttpMethod.Get, StatsPath, HttpStatusCode.BadRequest, "year_out_of_range");
        var page = Render<ProfileStats>(p => p.Add(x => x.Username, "ninja"));

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Geçersiz tarih."));
        HasButton(page, "Tekrar dene").Should().BeFalse();
    }

    private static string? SelectedYear(IRenderedComponent<ProfileStats> page) => page.Find("#stats-year").GetAttribute("value");
}
