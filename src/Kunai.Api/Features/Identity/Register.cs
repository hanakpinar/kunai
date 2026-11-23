using System.ComponentModel.DataAnnotations;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class Register
{
    public static void Map(RouteGroupBuilder auth) =>
        auth.MapPost("/register", HandleAsync)
            .WithName("Register")
            .AllowAnonymous()
            .WithSummary("Create an account")
            .WithDescription("Always answers 202 for a valid request, even when the email already has an account; the confirmation link arrives by email.")
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

    internal static async Task<Results<Accepted, ProblemHttpResult>> HandleAsync(
        RegisterRequest request,
        UserManager<AppUser> users,
        IdentityEmails emails,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var username = UsernameRules.Normalize(request.Username!);
        if (UsernameRules.Validate(username) is { } invalid)
        {
            return invalid.ToProblem();
        }

        if (await users.FindByNameAsync(username) is not null)
        {
            return Error.Conflict(UsernameRules.TakenCode, "The username is already taken.").ToProblem();
        }

        var email = request.Email!.Trim();
        if (await users.FindByEmailAsync(email) is { } existing)
        {
            // Do not reveal that the email is registered; tell its owner instead.
            await emails.SendAccountExistsAsync(existing, cancellationToken);
            return TypedResults.Accepted((string?)null);
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = username,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
            Locale = request.Locale ?? "tr",
            CreatedAt = time.GetUtcNow(),
        };

        var created = await users.CreateAsync(user, request.Password!);
        if (!created.Succeeded)
        {
            return IdentityErrors.From(created.Errors).ToProblem();
        }

        await emails.SendConfirmationAsync(user, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }
}

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "required")]
    [EmailAddress(ErrorMessage = "email_invalid")]
    [StringLength(254, ErrorMessage = "too_long")]
    public string? Email { get; init; }

    /// <summary>3-24 characters: lowercase letters, digits and underscores, starting with a letter.</summary>
    [Required(ErrorMessage = "required")]
    [StringLength(24, ErrorMessage = "too_long")]
    public string? Username { get; init; }

    /// <summary>12-128 characters. Passwords found in known breaches are rejected.</summary>
    [Required(ErrorMessage = "required")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "password_length")]
    public string? Password { get; init; }

    [StringLength(AppUser.DisplayNameMaxLength, ErrorMessage = "too_long")]
    public string? DisplayName { get; init; }

    /// <summary>"tr" (default) or "en"; used for emails.</summary>
    [AllowedValues("tr", "en", null, ErrorMessage = "locale_invalid")]
    public string? Locale { get; init; }
}
