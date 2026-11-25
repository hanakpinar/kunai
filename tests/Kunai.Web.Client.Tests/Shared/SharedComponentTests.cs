using System.Net;

using Bunit;

using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Components;
using Kunai.Web.Client.Infrastructure;
using Kunai.Web.Client.Tests.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Shared;

public sealed class SharedComponentTests : KunaiTestContext
{
    private static readonly Guid ItemId = Guid.Parse("0192f8a4-7c1e-7d4a-9b8e-3f2a1c0d5e6f");

    [Fact]
    public void A_status_badge_falls_back_to_a_neutral_label_when_the_registry_is_down()
    {
        Api.OnProblem(HttpMethod.Get, "/api/v1/item-types", HttpStatusCode.ServiceUnavailable, "service_unavailable");

        var badge = Render<StatusBadge>(p => p.Add(x => x.Type, ItemType.Book).Add(x => x.Status, ShelfStatus.InProgress));

        badge.WaitForAssertion(() => badge.Find(".status-badge").TextContent.Should().Be("Devam eden"));
    }

    [Fact]
    public void The_editor_shows_the_problem_and_retries_when_the_registry_fails()
    {
        Api.OnProblem(HttpMethod.Get, "/api/v1/item-types", HttpStatusCode.ServiceUnavailable, "service_unavailable");
        var editor = RenderEditor();

        editor.WaitForAssertion(() => editor.Find(".alert--error").TextContent.Should().Contain("tekrar dene"));
        editor.FindAll("select").Should().BeEmpty();

        Api.On(HttpMethod.Get, "/api/v1/item-types", TestCatalog.ItemTypes);
        editor.FindAll("button").Single(b => b.TextContent.Contains("Tekrar dene", StringComparison.Ordinal)).Click();

        editor.WaitForAssertion(() => editor.Find("select").TextContent.Should().Contain("Okuyacağım"));
        editor.FindAll(".alert--error").Should().BeEmpty();
    }

    [Fact]
    public async Task An_expired_session_on_save_signs_the_user_out()
    {
        SignInAs("ninja");
        var session = Services.GetRequiredService<AuthSession>();
        await session.RefreshAsync();
        session.Me.Should().NotBeNull();
        Api.OnProblem(HttpMethod.Put, $"/api/v1/me/shelf/{ItemId}", HttpStatusCode.Unauthorized, "unauthorized");
        Api.OnProblem(HttpMethod.Get, "/api/v1/me", HttpStatusCode.Unauthorized, "unauthorized");
        var editor = RenderEditor();
        editor.WaitForElement("form");

        editor.Find("form").Submit();

        editor.WaitForAssertion(() => session.Me.Should().BeNull("a 401 reloads the session so the router can send the user to sign in"));
        editor.Find(".alert--error").TextContent.Should().Contain("giriş yap");
    }

    private IRenderedComponent<ShelfEntryEditor> RenderEditor() => Render<ShelfEntryEditor>(p => p
        .Add(x => x.ItemId, ItemId)
        .Add(x => x.ItemType, ItemType.Book)
        .Add(x => x.Title, "Rafa ekle: Dune"));
}
