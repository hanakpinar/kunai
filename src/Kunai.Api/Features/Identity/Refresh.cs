using System.ComponentModel.DataAnnotations;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity.Sessions;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class Refresh
{
    public static void Map(RouteGroupBuilder auth) =>
        auth.MapPost("/refresh", HandleAsync)
            .WithName("RefreshTokens")
            .WithSummary("Exchange a refresh token for a new access token and refresh token")
            .WithDescription("Refresh tokens are single use. Presenting a used token again ends the session on every device that shares it.")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Refresh)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    internal static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> HandleAsync(
        RefreshRequest request,
        SessionService sessions,
        UserManager<AppUser> users,
        AccessTokenIssuer tokens,
        CancellationToken cancellationToken)
    {
        var rotated = await sessions.RotateAsync(request.RefreshToken!, cancellationToken);
        if (!rotated.IsSuccess)
        {
            return rotated.Error.ToProblem();
        }

        var (session, refreshToken) = rotated.Value;
        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (user is null)
        {
            await sessions.RevokeAsync(session.Id, cancellationToken);
            return Error.Unauthorized(SessionService.InvalidRefreshToken, "Sign in again.").ToProblem();
        }

        var (accessToken, expiresIn) = tokens.Issue(user, session.Id, await users.GetRolesAsync(user));
        return TypedResults.Ok(new TokenResponse(accessToken, "Bearer", expiresIn, refreshToken));
    }
}

public sealed class RefreshRequest
{
    [Required(ErrorMessage = "required")]
    [StringLength(128, ErrorMessage = "too_long")]
    public string? RefreshToken { get; init; }
}
