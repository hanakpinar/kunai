using System.Net;
using System.Text.Json.Nodes;

using Bunit;

using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Pages.Search.Items;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Search;

public sealed class ManualBookTests : KunaiTestContext
{
    private static readonly Guid ExistingId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-0000000000aa");
    private static readonly Guid NewId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-0000000000bb");

    [Fact]
    public void A_duplicate_isbn_links_to_the_existing_book_and_a_new_one_opens_its_pending_page()
    {
        SignInAs("ninja");
        Api.On(HttpMethod.Post, "/api/v1/items", new
        {
            status = 409,
            code = "book_exists",
            detail = $"A book with this ISBN is already in the catalog: {ExistingId}.",
        }, HttpStatusCode.Conflict);
        var page = Render<ManualBook>();

        page.Find("label:nth-of-type(1) input").Change("Kürk Mantolu Madonna");
        page.Find("input[placeholder='Orhan Pamuk, Elif Şafak']").Change("Sabahattin Ali, , Sabahattin Ali");
        page.FindAll("input[type=number]")[0].Change("1943");
        page.FindAll("input[type=number]")[1].Change("160");
        page.Find("input[inputmode=numeric]").Change("978-975-363-802-9");
        page.Find("form").Submit();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("Bu ISBN'e sahip kitap zaten katalogda"));
        page.Find($"a[href='items/{ExistingId}']").TextContent.Should().Contain("Katalogdaki kitabı aç");
        var body = JsonNode.Parse(Api.BodiesSentTo(HttpMethod.Post, "/api/v1/items").Single()!)!;
        body["title"]!.GetValue<string>().Should().Be("Kürk Mantolu Madonna");
        body["authors"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal("Sabahattin Ali");
        body["isbn13"]!.GetValue<string>().Should().Be("9789753638029");
        body["year"]!.GetValue<int>().Should().Be(1943);
        body["pageCount"]!.GetValue<int>().Should().Be(160);

        Api.On(HttpMethod.Post, "/api/v1/items", ItemJson.Book(NewId, catalogStatus: "pendingReview", source: "manual"), HttpStatusCode.Created);
        page.Find("input[inputmode=numeric]").Change(string.Empty);
        page.Find("form").Submit();

        page.WaitForAssertion(() => Services.GetRequiredService<NavigationManager>().Uri.Should().Be($"http://localhost/items/{NewId}?added=true"));
        JsonNode.Parse(Api.BodiesSentTo(HttpMethod.Post, "/api/v1/items").Last()!)!["isbn13"].Should().BeNull();
    }

    [Fact]
    public async Task An_expired_session_sends_the_user_to_sign_in_and_back_to_the_form()
    {
        RunningApp.UseRealAuthorization(this);
        SignInAs("ninja");
        var session = Services.GetRequiredService<AuthSession>();
        (await session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeTrue();

        // The cookie expired after the router read the session.
        Api.OnProblem(HttpMethod.Get, "/api/v1/me", HttpStatusCode.Unauthorized, "unauthorized");
        Api.OnProblem(HttpMethod.Post, "/api/v1/items", HttpStatusCode.Unauthorized, "unauthorized");
        var app = RunningApp.Render(this, "items/new");
        var navigation = Services.GetRequiredService<NavigationManager>();

        app.WaitForElement(".manual-book label:nth-of-type(1) input").Change("Kürk Mantolu Madonna");
        app.Find(".manual-book form").Submit();

        app.WaitForAssertion(() => navigation.Uri.Should().Be("http://localhost/login?returnUrl=items%2Fnew"));
        Api.BodiesSentTo(HttpMethod.Post, "/api/v1/items").Should().ContainSingle();
        Api.BodiesSentTo(HttpMethod.Get, "/api/v1/me").Should().HaveCount(2, "the 401 reloads the session");
        (await session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }
}
