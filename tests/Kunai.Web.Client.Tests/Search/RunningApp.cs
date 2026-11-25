using Bunit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Web.Client.Tests.Search;

/// <summary>The real router, to see where AuthorizeRouteView sends a page whose session ended.</summary>
internal static class RunningApp
{
    /// <summary>
    /// bUnit puts a placeholder in front of AddAuthorizationCore's service; the real router needs the real one.
    /// Call it before the first service is resolved.
    /// </summary>
    public static void UseRealAuthorization(BunitContext context) =>
        context.Services.AddSingleton<IAuthorizationService, DefaultAuthorizationService>();

    public static IRenderedComponent<Routes> Render(BunitContext context, string path)
    {
        // FocusOnNavigate moves the focus to the page's heading through JS.
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return context.Render<Routes>();
    }
}
