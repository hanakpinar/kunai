using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Caching;
using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity;
using Kunai.Api.Features.Profiles.Avatars;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Hybrid;

namespace Kunai.Api.Features.Profiles;

/// <summary>PUT /me/profile, /me/settings and /me/username. Each replaces its whole resource.</summary>
public static class UpdateMe
{
    public const string UsernameCooldown = "username_change_cooldown";
    public static readonly TimeSpan UsernameChangeInterval = TimeSpan.FromDays(30);

    public static void Map(RouteGroupBuilder me)
    {
        me.MapPut("/profile", UpdateProfileAsync)
            .WithName("PutMyProfile")
            .WithSummary("Display name and bio")
            .ProducesValidationProblem();

        me.MapPut("/settings", UpdateSettingsAsync)
            .WithName("PutMySettings")
            .WithSummary("Profile visibility, language, time zone and avatar")
            .ProducesValidationProblem();

        me.MapPut("/username", ChangeUsernameAsync)
            .WithName("PutMyUsername")
            .WithSummary("Change the username (at most once every 30 days)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    internal static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> UpdateProfileAsync(
        ProfileRequest request, ClaimsPrincipal principal, AppDbContext db, HybridCache cache, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([principal.GetUserId()], cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized(ErrorCodes.Unauthorized, "The account no longer exists.").ToProblem();
        }

        user.DisplayName = request.DisplayName!.Trim();
        user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(CacheTags.User(user.Id), cancellationToken);
        return await GetMe.HandleAsync(principal, db, cancellationToken);
    }

    internal static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> UpdateSettingsAsync(
        SettingsRequest request, ClaimsPrincipal principal, AppDbContext db, HybridCache cache, CancellationToken cancellationToken)
    {
        if (!UserClock.IsValidTimeZone(request.TimeZone))
        {
            return Error.ValidationField("timeZone", "time_zone_invalid", "Use an IANA time zone such as Europe/Istanbul.").ToProblem();
        }

        if (!AvatarRegistry.Exists(request.AvatarId))
        {
            return Error.ValidationField("avatarId", "avatar_invalid", "Choose one of ninja-01 ... ninja-12.").ToProblem();
        }

        var user = await db.Users.FindAsync([principal.GetUserId()], cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized(ErrorCodes.Unauthorized, "The account no longer exists.").ToProblem();
        }

        user.IsProfilePublic = request.IsProfilePublic;
        user.Locale = request.Locale!;
        user.TimeZone = request.TimeZone!;
        user.AvatarId = request.AvatarId!;
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(CacheTags.User(user.Id), cancellationToken);
        return await GetMe.HandleAsync(principal, db, cancellationToken);
    }

    internal static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> ChangeUsernameAsync(
        UsernameRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        AppDbContext db,
        HybridCache cache,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var username = UsernameRules.Normalize(request.Username!);
        if (UsernameRules.Validate(username) is { } invalid)
        {
            return invalid.ToProblem();
        }

        var user = await users.FindByIdAsync(principal.GetUserId().ToString());
        if (user is null)
        {
            return Error.Unauthorized(ErrorCodes.Unauthorized, "The account no longer exists.").ToProblem();
        }

        if (string.Equals(user.UserName, username, StringComparison.Ordinal))
        {
            return await GetMe.HandleAsync(principal, db, cancellationToken);
        }

        var now = time.GetUtcNow();
        if (user.UsernameChangedAt is { } changed && now - changed < UsernameChangeInterval)
        {
            return Error.BusinessRule(UsernameCooldown, $"The username can be changed again after {changed.Add(UsernameChangeInterval):yyyy-MM-dd}.").ToProblem();
        }

        user.UsernameChangedAt = now;
        var result = await users.SetUserNameAsync(user, username);
        if (!result.Succeeded)
        {
            return IdentityErrors.From(result.Errors).ToProblem();
        }

        await cache.RemoveByTagAsync(CacheTags.User(user.Id), cancellationToken);
        return await GetMe.HandleAsync(principal, db, cancellationToken);
    }
}

public sealed class ProfileRequest
{
    [Required(ErrorMessage = "required")]
    [StringLength(AppUser.DisplayNameMaxLength, MinimumLength = 1, ErrorMessage = "display_name_length")]
    public string? DisplayName { get; init; }

    [StringLength(AppUser.BioMaxLength, ErrorMessage = "too_long")]
    public string? Bio { get; init; }
}

public sealed class SettingsRequest
{
    /// <summary>Private profiles answer 404 to everyone else.</summary>
    public bool IsProfilePublic { get; init; }

    [Required(ErrorMessage = "required")]
    [AllowedValues("tr", "en", ErrorMessage = "locale_invalid")]
    public string? Locale { get; init; }

    /// <summary>IANA time zone, e.g. Europe/Istanbul.</summary>
    [Required(ErrorMessage = "required")]
    [StringLength(64, ErrorMessage = "too_long")]
    public string? TimeZone { get; init; }

    /// <summary>ninja-01 ... ninja-12.</summary>
    [Required(ErrorMessage = "required")]
    public string? AvatarId { get; init; }
}

public sealed class UsernameRequest
{
    [Required(ErrorMessage = "required")]
    [StringLength(24, ErrorMessage = "too_long")]
    public string? Username { get; init; }
}
