using System.Security.Claims;

using Kunai.Api.Features.Identity.Sessions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class Logout
{
    public static void Map(RouteGroupBuilder auth)
    {
        auth.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("End the current session (this device)");

        auth.MapPost("/logout-all", LogoutAllAsync)
            .WithName("LogoutAll")
            .WithSummary("End every session of the signed-in user (all devices)");
    }

    internal static async Task<NoContent> LogoutAsync(
        ClaimsPrincipal user,
        SessionService sessions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (user.GetSessionId() is { } sessionId)
        {
            await sessions.RevokeAsync(sessionId, cancellationToken);
        }

        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return TypedResults.NoContent();
    }

    internal static async Task<NoContent> LogoutAllAsync(
        ClaimsPrincipal principal,
        SessionService sessions,
        UserManager<AppUser> users,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        await sessions.RevokeAllAsync(userId, cancellationToken);

        if (await users.FindByIdAsync(userId.ToString()) is { } user)
        {
            // Invalidates outstanding password-reset and email tokens as well.
            await users.UpdateSecurityStampAsync(user);
        }

        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return TypedResults.NoContent();
    }
}
