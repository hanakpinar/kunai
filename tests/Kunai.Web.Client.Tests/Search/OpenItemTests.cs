using System.Net;

using Bunit;

using Kunai.Web.Client.Pages.Search.Items;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Search;

public sealed class OpenItemTests : KunaiTestContext
{
    private static readonly Guid DuneId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-3b2c4d5e6f70");

    [Fact]
    public void Imports_the_hit_then_replaces_the_address_with_its_page()
    {
        SignInAs("ninja");
        Api.On(HttpMethod.Post, "/api/v1/items/import", ItemJson.Movie(DuneId), HttpStatusCode.Created);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("items/open?ref=tmdb%3Amovie%3A438631");

        var page = Render<OpenItem>();

        page.WaitForAssertion(() => navigation.Uri.Should().Be($"http://localhost/items/{DuneId}"));
        Api.BodiesSentTo(HttpMethod.Post, "/api/v1/items/import").Single().Should().Be("{\"ref\":\"tmdb:movie:438631\"}");
    }

    [Fact]
    public void An_unknown_ref_shows_why_and_no_retry_that_cannot_work()
    {
        SignInAs("ninja");
        Api.OnProblem(HttpMethod.Post, "/api/v1/items/import", HttpStatusCode.UnprocessableEntity, "external_item_not_found");
        Services.GetRequiredService<NavigationManager>().NavigateTo("items/open?ref=tmdb%3Amovie%3A1");

        var page = Render<OpenItem>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("İçerik kaynakta bulunamadı"));
        page.FindAll("button").Should().BeEmpty();
        page.Find("a[href='search']").TextContent.Should().Be("Aramaya dön");
    }
}
