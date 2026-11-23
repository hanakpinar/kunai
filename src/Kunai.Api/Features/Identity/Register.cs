using System.ComponentModel.DataAnnotations;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Profiles.Avatars;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Npgsql;

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

        // Check the password before looking at the email, so a weak password gets the same 400 whether or not
        // the email is registered.
        if (await PasswordErrorAsync(users, username, email, request.Password!) is { } weak)
        {
            return weak.ToProblem();
        }

        if (await users.FindByEmailAsync(email) is { } existing)
        {
            // Do not reveal that the email is registered; tell its owner instead.
            await emails.SendAccountExistsAsync(existing, CancellationToken.None);
            return TypedResults.Accepted((string?)null);
        }

        var userId = Guid.CreateVersion7();
        var user = new AppUser
        {
            Id = userId,
            AvatarId = AvatarRegistry.DefaultFor(userId),
            UserName = username,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
            Locale = request.Locale ?? "tr",
            CreatedAt = time.GetUtcNow(),
        };

        var created = await CreateAsync(users, user, request.Password!);
        if (created is not null)
        {
            return created;
        }

        // The account exists now; a client that disconnects must not stop its confirmation email.
        await emails.SendConfirmationAsync(user, CancellationToken.None);
        return TypedResults.Accepted((string?)null);
    }

    /// <returns>null on success, otherwise the response to send.</returns>
    private static async Task<Results<Accepted, ProblemHttpResult>?> CreateAsync(UserManager<AppUser> users, AppUser user, string password)
    {
        try
        {
            var created = await users.CreateAsync(user, password);
            if (created.Succeeded)
            {
                return null;
            }

            return IdentityErrors.From(created.Errors).ToProblem();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique)
        {
            // A parallel registration won the race; answer as if we had checked first.
            return string.Equals(unique.ConstraintName, "EmailIndex", StringComparison.Ordinal)
                ? TypedResults.Accepted((string?)null)
                : Error.Conflict(UsernameRules.TakenCode, "The username is already taken.").ToProblem();
        }
    }

    private static async Task<Error?> PasswordErrorAsync(UserManager<AppUser> users, string username, string email, string password)
    {
        var candidate = new AppUser { UserName = username, Email = email };
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, candidate, password);
            if (!result.Succeeded)
            {
                return IdentityErrors.From(result.Errors);
            }
        }

        return null;
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
