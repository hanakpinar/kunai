using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Kunai.Api.Features.Identity.Sessions;

/// <summary>Rejects cookies and access tokens whose session has been revoked or has expired.</summary>
internal static class SessionGuard
{
    public static async Task ValidateTokenAsync(TokenValidatedContext context)
    {
        if (!await IsActiveAsync(context.Principal, context.HttpContext))
        {
            context.Fail("The session has ended.");
        }
    }

    public static async Task ValidateCookieAsync(CookieValidatePrincipalContext context)
    {
        if (!await IsActiveAsync(context.Principal, context.HttpContext))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }

    private static async Task<bool> IsActiveAsync(ClaimsPrincipal? principal, HttpContext httpContext) =>
        Guid.TryParse(principal?.FindFirstValue(KunaiClaims.SessionId), out var sessionId)
        && await httpContext.RequestServices.GetRequiredService<SessionService>()
            .IsActiveAsync(sessionId, httpContext.RequestAborted);
}
