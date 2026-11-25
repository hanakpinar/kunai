using System.Net;

using Bunit;
using Bunit.Extensions.WaitForHelpers;

using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Pages.Search.Items;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Search;

public sealed class OpenItemTests : KunaiTestContext
{
    private const string ImportPath = "/api/v1/items/import";
    private static readonly Guid DuneId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-3b2c4d5e6f70");
    private static readonly Guid OtherId = Guid.Parse("0193a5f0-7c1e-7d2a-9f10-3b2c4d5e6f71");

    [Fact]
    public void Imports_the_hit_then_replaces_the_address_with_its_page()
    {
        SignInAs("ninja");
        Api.On(HttpMethod.Post, ImportPath, ItemJson.Movie(DuneId), HttpStatusCode.Created);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("items/open?ref=tmdb%3Amovie%3A438631");

        var page = Render<OpenItem>();

        page.WaitForAssertion(() => navigation.Uri.Should().Be($"http://localhost/items/{DuneId}"));
        Api.BodiesSentTo(HttpMethod.Post, ImportPath).Single().Should().Be("{\"ref\":\"tmdb:movie:438631\"}");
    }

    [Fact]
    public void Rendering_again_with_the_same_ref_does_not_start_another_import()
    {
        SignInAs("ninja");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HoldBack.In(this, Api).Path(ImportPath, release.Task);
        Api.On(HttpMethod.Post, ImportPath, ItemJson.Movie(DuneId), HttpStatusCode.Created);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("items/open?ref=tmdb%3Amovie%3A438631");
        var page = Render<OpenItem>();

        // The layout renders the page again (a session refresh, say) while the import runs.
        page.Render();
        release.SetResult();

        page.WaitForAssertion(() => navigation.Uri.Should().Be($"http://localhost/items/{DuneId}"));
        Api.BodiesSentTo(HttpMethod.Post, ImportPath).Should().ContainSingle();
    }

    [Fact]
    public void An_unknown_ref_shows_why_and_no_retry_that_cannot_work()
    {
        SignInAs("ninja");
        Api.OnProblem(HttpMethod.Post, ImportPath, HttpStatusCode.UnprocessableEntity, "external_item_not_found");
        Services.GetRequiredService<NavigationManager>().NavigateTo("items/open?ref=tmdb%3Amovie%3A1");

        var page = Render<OpenItem>();

        page.WaitForAssertion(() => page.Find(".alert--error").TextContent.Should().Contain("İçerik kaynakta bulunamadı"));
        page.FindAll("button").Should().BeEmpty();
        page.Find("a[href='search']").TextContent.Should().Be("Aramaya dön");
    }

    [Fact]
    public async Task Leaving_while_the_import_runs_keeps_the_user_on_the_page_they_went_to()
    {
        SignInAs("ninja");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HoldBack.In(this, Api).Path(ImportPath, release.Task);
        Api.On(HttpMethod.Post, ImportPath, ItemJson.Movie(DuneId), HttpStatusCode.Created);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("items/open?ref=tmdb%3Amovie%3A438631");
        var page = Render<OpenItem>();
        page.WaitForAssertion(() => page.Find(".spinner").Should().NotBeNull());

        // The user opens their shelf: the router disposes this page and shows the other one.
        await DisposeComponentsAsync();
        navigation.NavigateTo("shelf");
        var moved = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        navigation.LocationChanged += (_, e) => moved.TrySetResult(e.Location);
        release.SetResult();

        var first = await Task.WhenAny(moved.Task, Task.Delay(TimeSpan.FromMilliseconds(500), Xunit.TestContext.Current.CancellationToken));
        first.Should().NotBeSameAs(moved.Task, "the import finished after the user left its page");
        navigation.Uri.Should().Be("http://localhost/shelf");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_ref_that_replaced_another_ignores_the_old_import_when_it_answers_last(bool oldImportFails)
    {
        SignInAs("ninja");
        var releaseOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseNew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HoldBack.In(this, Api).BodyContaining("tmdb:movie:1\"", releaseOld.Task).BodyContaining("tmdb:movie:2\"", releaseNew.Task);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("items/open?ref=tmdb%3Amovie%3A1");
        var page = Render<OpenItem>();

        // Back/forward to another hit reuses the page while the first import is still running.
        navigation.NavigateTo("items/open?ref=tmdb%3Amovie%3A2");
        if (oldImportFails)
        {
            Api.OnProblem(HttpMethod.Post, ImportPath, HttpStatusCode.InternalServerError, "internal_error");
        }
        else
        {
            Api.On(HttpMethod.Post, ImportPath, ItemJson.Movie(OtherId), HttpStatusCode.Created);
        }

        releaseOld.SetResult();

        var changed = () => page.WaitForState(
            () => page.FindAll(".alert--error").Count > 0 || !navigation.Uri.EndsWith("ref=tmdb%3Amovie%3A2", StringComparison.Ordinal),
            TimeSpan.FromMilliseconds(300));
        changed.Should().Throw<WaitForFailedException>("the old ref's answer belongs to the page it left");
        Api.BodiesSentTo(HttpMethod.Post, ImportPath).Should().ContainSingle().Which.Should().Contain("tmdb:movie:1");

        Api.On(HttpMethod.Post, ImportPath, ItemJson.Movie(DuneId), HttpStatusCode.Created);
        releaseNew.SetResult();
        page.WaitForAssertion(() => navigation.Uri.Should().Be($"http://localhost/items/{DuneId}"));
    }

    [Fact]
    public async Task An_expired_session_sends_the_user_to_sign_in_and_back_here()
    {
        RunningApp.UseRealAuthorization(this);
        SignInAs("ninja");
        var session = Services.GetRequiredService<AuthSession>();
        (await session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeTrue();

        // The cookie expired after the router read the session.
        Api.OnProblem(HttpMethod.Get, "/api/v1/me", HttpStatusCode.Unauthorized, "unauthorized");
        Api.OnProblem(HttpMethod.Post, ImportPath, HttpStatusCode.Unauthorized, "unauthorized");
        var app = RunningApp.Render(this, "items/open?ref=tmdb%3Amovie%3A438631");
        var navigation = Services.GetRequiredService<NavigationManager>();

        app.WaitForAssertion(() => navigation.Uri.Should().Be($"http://localhost/login?returnUrl={Uri.EscapeDataString("items/open?ref=tmdb%3Amovie%3A438631")}"));
        Api.BodiesSentTo(HttpMethod.Get, "/api/v1/me").Should().HaveCount(2, "the 401 reloads the session");
        (await session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }
}
