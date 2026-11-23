using System.Security.Claims;

using Kunai.Api.Common.Caching;
using Kunai.Api.Common.Http;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;

namespace Kunai.Api.Features.Profiles;

public static class GetPublicProfile
{
    private static readonly HybridCacheEntryOptions TenMinutes = new() { Expiration = TimeSpan.FromMinutes(10) };

    public static void Map(RouteGroupBuilder users) =>
        users.MapGet("/{username}", HandleAsync)
            .WithName("GetPublicProfile")
            .WithSummary("A user's public profile: belt, counts, this year and favorites")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<Results<Ok<PublicProfileResponse>, ProblemHttpResult>> HandleAsync(
        string username,
        ClaimsPrincipal viewer,
        AppDbContext db,
        HybridCache cache,
        TimeProvider time,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await ProfileLookup.FindVisibleAsync(db, username, viewer, cancellationToken);
        if (user is null)
        {
            return ProfileLookup.NotFound.ToProblem();
        }

        var profile = await cache.GetOrCreateAsync(
            $"profile:{user.Id}",
            async token => await ProfileSummary.ComputeAsync(db, user, time, token),
            TenMinutes,
            [CacheTags.User(user.Id)],
            cancellationToken);

        httpContext.Response.Headers.CacheControl = "no-cache";
        return TypedResults.Ok(profile);
    }
}
