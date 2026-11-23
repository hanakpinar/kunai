using System.Security.Claims;

namespace Kunai.Api.Features.Identity;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id. Only call on endpoints that require authentication.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(KunaiClaims.UserId)
            ?? throw new InvalidOperationException("The principal has no user id claim."));

    public static Guid? GetSessionId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(KunaiClaims.SessionId), out var id) ? id : null;
}
