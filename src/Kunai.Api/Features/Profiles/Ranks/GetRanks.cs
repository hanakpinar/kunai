using Microsoft.AspNetCore.Http.HttpResults;

namespace Kunai.Api.Features.Profiles.Ranks;

public static class GetRanks
{
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet("/ranks", Handle)
            .WithTags("Profiles")
            .WithName("GetRanks")
            .WithSummary("Belt ranks with TR/EN names, point thresholds and colors")
            .AllowAnonymous();

    internal static Ok<IReadOnlyList<Belt>> Handle(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "public, max-age=3600";
        return TypedResults.Ok(Belts.All);
    }
}
