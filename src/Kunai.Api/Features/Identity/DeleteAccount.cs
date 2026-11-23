using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Caching;
using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;

namespace Kunai.Api.Features.Identity;

/// <summary>
/// Permanently deletes the account and everything that belongs to it (sessions, shelf, goals) through
/// database cascades. Required by the App Store and Google Play; also covers KVKK/GDPR erasure.
/// Manually added catalog items stay, without a link to the deleted account.
/// </summary>
public static class DeleteAccount
{
    public static void Map(RouteGroupBuilder me) =>
        me.MapDelete("/", HandleAsync)
            .WithName("DeleteMyAccount")
            .WithSummary("Delete my account permanently (asks for the password again)")
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem();

    internal static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        [FromBody] DeleteAccountRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        HybridCache cache,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(principal.GetUserId().ToString());
        if (user is null)
        {
            return TypedResults.NoContent();
        }

        if (!await users.CheckPasswordAsync(user, request.Password!))
        {
            return Error.ValidationField("password", "password_mismatch", "The password is wrong.").ToProblem();
        }

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return IdentityErrors.From(result.Errors).ToProblem();
        }

        await cache.RemoveByTagAsync(CacheTags.User(user.Id), cancellationToken);
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return TypedResults.NoContent();
    }
}

public sealed class DeleteAccountRequest
{
    [Required(ErrorMessage = "required")]
    [StringLength(128, ErrorMessage = "too_long")]
    public string? Password { get; init; }
}
