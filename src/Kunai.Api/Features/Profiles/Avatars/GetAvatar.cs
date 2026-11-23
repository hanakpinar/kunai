using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Profiles.Ranks;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Kunai.Api.Features.Profiles.Avatars;

public static class GetAvatar
{
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet("/avatars/{file}", Handle)
            .WithTags("Profiles")
            .WithName("GetAvatar")
            .WithSummary("A built-in ninja avatar as SVG, its belt painted in the given belt color")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK, contentType: "image/svg+xml")
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static Results<ContentHttpResult, ProblemHttpResult> Handle(string file, string? belt, HttpContext httpContext)
    {
        var avatarId = file.EndsWith(".svg", StringComparison.Ordinal) ? file[..^4] : file;
        if (!AvatarRegistry.Exists(avatarId))
        {
            return Error.NotFound("avatar_not_found", "No such avatar.").ToProblem();
        }

        var color = (Belts.Find(belt) ?? Belts.All[0]).Color;
        httpContext.Response.Headers.CacheControl = "public, max-age=86400";
        return TypedResults.Content(AvatarSvg.Render(avatarId, color), "image/svg+xml");
    }
}
