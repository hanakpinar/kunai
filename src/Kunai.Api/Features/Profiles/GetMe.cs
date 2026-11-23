using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Profiles;

public static class GetMe
{
    public static void Map(RouteGroupBuilder me) =>
        me.MapGet("/", HandleAsync)
            .WithName("GetMe")
            .WithSummary("The signed-in user's account and settings");

    internal static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> HandleAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var me = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new MeResponse(
                u.Id,
                u.UserName!,
                u.Email!,
                u.DisplayName,
                u.Bio,
                u.AvatarId,
                u.IsProfilePublic,
                u.Locale,
                u.TimeZone,
                u.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return me is null
            ? Error.Unauthorized(ErrorCodes.Unauthorized, "The account no longer exists.").ToProblem()
            : TypedResults.Ok(me);
    }
}

public sealed record MeResponse(
    Guid Id,
    string Username,
    string Email,
    string DisplayName,
    string? Bio,
    string AvatarId,
    bool IsProfilePublic,
    string Locale,
    string TimeZone,
    DateTimeOffset CreatedAt);
