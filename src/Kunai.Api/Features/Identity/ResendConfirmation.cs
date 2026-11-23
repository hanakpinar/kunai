using System.ComponentModel.DataAnnotations;

using Kunai.Api.Common.Http;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class ResendConfirmation
{
    public static void Map(RouteGroupBuilder auth) =>
        auth.MapPost("/confirm-email/resend", HandleAsync)
            .WithName("ResendEmailConfirmation")
            .AllowAnonymous()
            .WithSummary("Send the confirmation email again")
            .WithDescription("Always answers 202, so it cannot be used to find out which emails are registered.")
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem();

    internal static async Task<Accepted> HandleAsync(
        ResendConfirmationRequest request,
        UserManager<AppUser> users,
        IdentityEmails emails,
        CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(request.Email!.Trim()) is { EmailConfirmed: false } user)
        {
            await emails.SendConfirmationAsync(user, cancellationToken);
        }

        return TypedResults.Accepted((string?)null);
    }
}

public sealed class ResendConfirmationRequest
{
    [Required(ErrorMessage = "required")]
    [EmailAddress(ErrorMessage = "email_invalid")]
    [StringLength(254, ErrorMessage = "too_long")]
    public string? Email { get; init; }
}
