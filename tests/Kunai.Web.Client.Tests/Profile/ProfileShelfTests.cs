using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Profile;

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

        page.WaitForAssertion(() => page.Find("#panel-Shelf .alert--error").TextContent.Should().Contain("Liste değişti"));
        HasButton(page, "Tekrar dene").Should().BeFalse("the same request would be refused again");
        page.FindAll("#panel-Shelf .item-card").Should().ContainSingle("the loaded page stays");
    }
}
