using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Profile;

using Microsoft.AspNetCore.Components;

namespace Kunai.Web.Client.Tests.Profile;

public sealed class ProfileShelfTests : ProfileTestContext
{
    private const string ShelfPath = $"{ProfilePath}/shelf";

    [Fact]
    public void Shelf_pages_with_the_next_cursor()
    {
        Serve();
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));

        page.WaitForAssertion(() => page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Okudum").And.Contain("9/10"));
        page.Find("#panel-Shelf .toolbar").TextContent.Should().Contain("2 içerik");
        Api.On(HttpMethod.Get, ShelfPath, ShelfPage("Hyperion", nextCursor: null));
        ButtonNamed(page, "Daha fazla").Click();

        page.WaitForAssertion(() => page.FindAll("#panel-Shelf .item-card").Should().HaveCount(2));
        QueriesTo(ShelfPath).Should().HaveCount(2);
        QueriesTo(ShelfPath).Last().Should().ContainEquivalentOf("cursor=next-page");
        HasButton(page, "Daha fazla").Should().BeFalse("the last page has no next cursor");
    }

    [Fact]
    public void A_filter_starts_again_from_its_first_page()
    {
        Serve();
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Dune"));
        HasButton(page, "Daha fazla").Should().BeTrue("the first page still has a next cursor");

        Api.On(HttpMethod.Get, ShelfPath, ShelfPage("Hyperion", nextCursor: "book-page-2", totalCount: 5));
        ButtonNamed(page, "Kitap").Click();

        page.WaitForAssertion(() => QueriesTo(ShelfPath).Should().HaveCount(2));
        QueriesTo(ShelfPath).Last().Should().ContainEquivalentOf("type=book").And.NotContainEquivalentOf("cursor=");
        page.WaitForAssertion(() => page.FindAll("#panel-Shelf .item-card").Should().ContainSingle()
            .Which.TextContent.Should().Contain("Hyperion"));
        page.Find("#panel-Shelf .toolbar").TextContent.Should().Contain("5 içerik");
        ButtonNamed(page, "Kitap").GetAttribute("aria-pressed").Should().Be("true");
        ButtonNamed(page, "Tümü").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void A_failed_filter_drops_the_old_count_and_offers_a_retry()
    {
        Serve();
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("#panel-Shelf .toolbar").TextContent.Should().Contain("2 içerik"));

        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.ServiceUnavailable, "service_unavailable");
        ButtonNamed(page, "Film").Click();

        page.WaitForAssertion(() => page.Find("#panel-Shelf .alert--error").TextContent.Should().Contain("Sunucuda bir hata oluştu"));
        page.Find("#panel-Shelf .toolbar").TextContent.Should().NotContain("içerik", "the count belonged to the previous filter");
        page.FindAll("#panel-Shelf .item-card").Should().BeEmpty();

        Api.On(HttpMethod.Get, ShelfPath, ShelfPage("Dune", nextCursor: null, totalCount: 1));
        ButtonNamed(page, "Tekrar dene").Click();

        page.WaitForAssertion(() => page.FindAll("#panel-Shelf .item-card").Should().ContainSingle());
        page.Find("#panel-Shelf .toolbar").TextContent.Should().Contain("1 içerik");
        page.FindAll("#panel-Shelf .alert--error").Should().BeEmpty();
        QueriesTo(ShelfPath).Last().Should().ContainEquivalentOf("type=movie").And.NotContainEquivalentOf("cursor=");
    }

    [Fact]
    public void A_rejected_page_does_not_offer_a_retry()
    {
        Serve();
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Dune"));

        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.BadRequest, "invalid_cursor");
        ButtonNamed(page, "Daha fazla").Click();

        page.WaitForAssertion(() => page.Find("#panel-Shelf .more .alert--error").TextContent.Should().Contain("Liste değişti"));
        HasButton(page, "Tekrar dene").Should().BeFalse("the same request would be refused again");
        HasButton(page, "Daha fazla").Should().BeFalse("it would send that request again");
        page.FindAll("#panel-Shelf .item-card").Should().ContainSingle("the loaded page stays");
    }

    [Fact]
    public void A_failed_next_page_shows_its_problem_by_the_button_that_asked_for_it()
    {
        Serve();
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForAssertion(() => page.Find("#panel-Shelf .item-card").TextContent.Should().Contain("Dune"));

        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.ServiceUnavailable, "service_unavailable");
        ButtonNamed(page, "Daha fazla").Click();

        // At the bottom of a long shelf, where the user clicked; "Daha fazla" there is the one retry.
        page.WaitForAssertion(() => page.Find("#panel-Shelf .more .alert--error").TextContent.Should().Contain("Sunucuda bir hata oluştu"));
        page.FindAll("#panel-Shelf .load-problem").Should().BeEmpty("nothing is shown above the shelf");
        HasButton(page, "Tekrar dene").Should().BeFalse();
        page.Find("#panel-Shelf .more .alert--error").NextElementSibling!.TextContent.Trim().Should().Be("Daha fazla");

        Api.On(HttpMethod.Get, ShelfPath, ShelfPage("Hyperion", nextCursor: null));
        ButtonNamed(page, "Daha fazla").Click();

        page.WaitForAssertion(() => page.FindAll("#panel-Shelf .item-card").Should().HaveCount(2));
        page.FindAll("#panel-Shelf .alert--error").Should().BeEmpty();
        QueriesTo(ShelfPath).Skip(1).Should().HaveCount(2).And.AllSatisfy(q => q.Should().ContainEquivalentOf("cursor=next-page"));
    }

    [Fact]
    public void Retrying_the_first_page_moves_the_focus_to_the_shelf_panel()
    {
        Api.AnswerAsynchronously = true;
        Serve();
        Api.OnProblem(HttpMethod.Get, ShelfPath, HttpStatusCode.ServiceUnavailable, "service_unavailable");
        var page = Render<PublicProfile>(p => p.Add(x => x.Username, "ninja"));
        page.WaitForElement("#panel-Shelf");
        var panel = page.Find("#panel-Shelf").GetAttribute("blazor:elementreference");
        page.Find("#panel-Shelf").GetAttribute("tabindex").Should().Be("-1");
        page.WaitForAssertion(() => page.Find("#panel-Shelf .alert--error"));

        Api.On(HttpMethod.Get, ShelfPath, ShelfPage("Dune", nextCursor: null));
        ButtonNamed(page, "Tekrar dene").Click();

        // "Tekrar dene", which had the focus, goes away with the problem: the panel takes it, not the top of the page.
        page.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke().Arguments[0].Should().BeOfType<ElementReference>()
            .Which.Id.Should().Be(panel));
        page.WaitForAssertion(() => page.FindAll("#panel-Shelf .item-card").Should().ContainSingle());
    }
}
