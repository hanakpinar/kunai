using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity.Sessions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class Login
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountLocked = "account_locked";
    public const string EmailNotConfirmed = "email_not_confirmed";

    public static void Map(RouteGroupBuilder auth) =>
        auth.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithSummary("Sign in with email or username and password")
            .WithDescription("Web apps send useCookie=true and receive an httpOnly cookie (204). Native apps receive an access token and a refresh token (200).")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    internal static async Task<Results<Ok<TokenResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        LoginRequest request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        SessionService sessions,
        AccessTokenIssuer tokens,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var invalid = Error.Unauthorized(InvalidCredentials, "The login or the password is wrong.");
        var login = request.Login!.Trim();
        var user = login.Contains('@', StringComparison.Ordinal)
            ? await users.FindByEmailAsync(login)
            : await users.FindByNameAsync(UsernameRules.Normalize(login));
        if (user is null)
        {
            return invalid.ToProblem();
        }

        var check = await signIn.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return Error.Forbidden(AccountLocked, "Too many failed attempts. Try again in 15 minutes.").ToProblem();
        }

        if (check.IsNotAllowed)
        {
            return Error.Forbidden(EmailNotConfirmed, "Confirm your email address before signing in.").ToProblem();
        }

        if (!check.Succeeded)
        {
            return invalid.ToProblem();
        }

        var clientType = request.UseCookie ? ClientType.Web : ClientType.Mobile;
        var session = await sessions.StartAsync(user.Id, clientType, request.DeviceName, cancellationToken);

        if (request.UseCookie)
        {
            var principal = await signIn.CreateUserPrincipalAsync(user);
            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(KunaiClaims.SessionId, session.Id.ToString()));
            await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties { IsPersistent = true });
            return TypedResults.NoContent();
        }

        var (accessToken, expiresIn) = tokens.Issue(user, session.Id, await users.GetRolesAsync(user));
        var refreshToken = await sessions.IssueRefreshTokenAsync(session, cancellationToken);
        return TypedResults.Ok(new TokenResponse(accessToken, "Bearer", expiresIn, refreshToken));
    }
}

public sealed class LoginRequest
{
    /// <summary>Email address or username.</summary>
    [Required(ErrorMessage = "required")]
    [StringLength(254, ErrorMessage = "too_long")]
    public string? Login { get; init; }

    [Required(ErrorMessage = "required")]
    [StringLength(128, ErrorMessage = "too_long")]
    public string? Password { get; init; }

    /// <summary>True for browsers: the session lives in an httpOnly cookie instead of tokens.</summary>
    public bool UseCookie { get; init; }

    /// <summary>Optional label shown in the device list, e.g. "iPhone 17".</summary>
    [StringLength(UserSession.DeviceNameMaxLength, ErrorMessage = "too_long")]
    public string? DeviceName { get; init; }
}

/// <param name="AccessToken">ES256 JWT; send it as "Authorization: Bearer ...".</param>
/// <param name="TokenType">Always "Bearer".</param>
/// <param name="ExpiresIn">Access token lifetime in seconds.</param>
/// <param name="RefreshToken">Single use. Exchange it at /auth/refresh before the session expires; never refresh twice in parallel.</param>
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken);
