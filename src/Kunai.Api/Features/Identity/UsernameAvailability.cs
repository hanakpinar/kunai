using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class UsernameAvailability
{
    public static void Map(RouteGroupBuilder auth) =>
        auth.MapGet("/username-availability", HandleAsync)
            .WithName("GetUsernameAvailability")
            .WithSummary("Check whether a username can be registered");

    internal static async Task<UsernameAvailabilityResponse> HandleAsync(string username, UserManager<AppUser> users)
    {
        var normalized = UsernameRules.Normalize(username);
        if (UsernameRules.Validate(normalized) is { } error)
        {
            return new UsernameAvailabilityResponse(normalized, false, error.Fields!["username"][0]);
        }

        return await users.FindByNameAsync(normalized) is null
            ? new UsernameAvailabilityResponse(normalized, true, null)
            : new UsernameAvailabilityResponse(normalized, false, UsernameRules.TakenCode);
    }
}

/// <param name="Username">The normalized (lowercase) username that was checked.</param>
/// <param name="Available">True when the username can be registered right now.</param>
/// <param name="Reason">username_invalid, username_reserved or username_taken when not available.</param>
public sealed record UsernameAvailabilityResponse(string Username, bool Available, string? Reason);
