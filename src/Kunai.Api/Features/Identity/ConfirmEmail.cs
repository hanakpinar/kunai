using System.ComponentModel.DataAnnotations;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class ConfirmEmail
{
    public static void Map(RouteGroupBuilder auth) =>
        auth.MapPost("/confirm-email", HandleAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirm an email address with the userId and token from the confirmation link")
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem();

    internal static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        ConfirmEmailRequest request,
        UserManager<AppUser> users)
    {
        var invalid = Error.Validation(IdentityErrors.InvalidToken, "The link is invalid or has expired.");

        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = IdentityEmails.DecodeToken(request.Token!);
        if (user is null || token is null)
        {
            return invalid.ToProblem();
        }

        if (user.EmailConfirmed)
        {
            return TypedResults.NoContent();
        }

        var result = await users.ConfirmEmailAsync(user, token);
        return result.Succeeded ? TypedResults.NoContent() : invalid.ToProblem();
    }
}

public sealed class ConfirmEmailRequest
{
    [Required(ErrorMessage = "required")]
    public Guid UserId { get; init; }

    [Required(ErrorMessage = "required")]
    [StringLength(2048, ErrorMessage = "too_long")]
    public string? Token { get; init; }
}
