using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

/// <summary>
/// Changing the password or username rotates the security stamp. The device that made the change keeps
/// its session, so its cookie is issued again with the new stamp and the same session id.
/// </summary>
internal static class CookieRenewal
{
    public static async Task RenewIfCookieAsync(HttpContext httpContext, ClaimsPrincipal caller, SignInManager<AppUser> signIn, AppUser user)
    {
        if (!string.Equals(caller.Identity?.AuthenticationType, IdentityConstants.ApplicationScheme, StringComparison.Ordinal)
            || caller.GetSessionId() is not { } sessionId)
        {
            return;
        }

        var principal = await signIn.CreateUserPrincipalAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(KunaiClaims.SessionId, sessionId.ToString()));
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties { IsPersistent = true });
    }
}
