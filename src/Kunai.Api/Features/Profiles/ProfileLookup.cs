using System.Security.Claims;

using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Profiles;

/// <summary>Finds a user by username if the viewer may see them: public profiles, or your own.</summary>
public static class ProfileLookup
{
    public const string NotFoundCode = "user_not_found";

    public static Error NotFound { get; } = Error.NotFound(NotFoundCode, "No such user, or the profile is private.");

    public static async Task<AppUser?> FindVisibleAsync(AppDbContext db, string username, ClaimsPrincipal viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(viewer);
        var normalized = UsernameRules.Normalize(username);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserName == normalized, cancellationToken);

        // Private profiles answer exactly like missing ones, so they cannot be discovered.
        return user is not null && (user.IsProfilePublic || IsOwner(viewer, user.Id)) ? user : null;
    }

    private static bool IsOwner(ClaimsPrincipal viewer, Guid userId) =>
        viewer.Identity?.IsAuthenticated == true && viewer.GetUserId() == userId;
}
