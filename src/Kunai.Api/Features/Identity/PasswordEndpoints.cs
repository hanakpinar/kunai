using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity.Sessions;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class PasswordEndpoints
{
    public static void MapAuth(RouteGroupBuilder auth)
    {
        auth.MapPost("/password/forgot", ForgotAsync)
            .WithName("ForgotPassword")
            .WithSummary("Email a password-reset link (valid for one hour)")
            .WithDescription("Always answers 202, so it cannot be used to find out which emails are registered.")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem();

        auth.MapPost("/password/reset", ResetAsync)
            .WithName("ResetPassword")
            .WithSummary("Set a new password with the userId and token from the reset link; signs out every device")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem();
    }

    public static void MapMe(RouteGroupBuilder me) =>
        me.MapPost("/password", ChangeAsync)
            .WithName("ChangeMyPassword")
            .WithSummary("Change the password; other devices are signed out")
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem();

    internal static async Task<Accepted> ForgotAsync(
        ForgotPasswordRequest request, UserManager<AppUser> users, IdentityEmails emails, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(request.Email!.Trim()) is { EmailConfirmed: true } user)
        {
            await emails.SendPasswordResetAsync(user, cancellationToken);
        }

        return TypedResults.Accepted((string?)null);
    }

    internal static async Task<Results<NoContent, ProblemHttpResult>> ResetAsync(
        ResetPasswordRequest request, UserManager<AppUser> users, SessionService sessions, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = IdentityEmails.DecodeToken(request.Token!);
        if (user is null || token is null)
        {
            return Error.Validation(IdentityErrors.InvalidToken, "The link is invalid or has expired.").ToProblem();
        }

        var result = await users.ResetPasswordAsync(user, token, request.NewPassword!);
        if (!result.Succeeded)
        {
            return IdentityErrors.From(result.Errors).ToProblem();
        }

        // Whoever knew the old password is signed out everywhere.
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, ProblemHttpResult>> ChangeAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        SessionService sessions,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(principal.GetUserId().ToString());
        if (user is null)
        {
            return Error.Unauthorized(ErrorCodes.Unauthorized, "The account no longer exists.").ToProblem();
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword!);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => string.Equals(e.Code, nameof(IdentityErrorDescriber.PasswordMismatch), StringComparison.Ordinal))
                ? Error.ValidationField("currentPassword", "password_mismatch", "The current password is wrong.").ToProblem()
                : IdentityErrors.From(result.Errors).ToProblem();
        }

        await sessions.RevokeAllExceptAsync(user.Id, principal.GetSessionId(), cancellationToken);
        return TypedResults.NoContent();
    }
}

public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "required")]
    [EmailAddress(ErrorMessage = "email_invalid")]
    [StringLength(254, ErrorMessage = "too_long")]
    public string? Email { get; init; }
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "required")]
    public Guid UserId { get; init; }

    [Required(ErrorMessage = "required")]
    [StringLength(2048, ErrorMessage = "too_long")]
    public string? Token { get; init; }

    [Required(ErrorMessage = "required")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "password_length")]
    public string? NewPassword { get; init; }
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "required")]
    [StringLength(128, ErrorMessage = "too_long")]
    public string? CurrentPassword { get; init; }

    [Required(ErrorMessage = "required")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "password_length")]
    public string? NewPassword { get; init; }
}
