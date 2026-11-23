using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Features.Library;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Kunai.Api.Features.Profiles;

public static class GetUserShelf
{
    public static void Map(RouteGroupBuilder users) =>
        users.MapGet("/{username}/shelf", HandleAsync)
            .WithName("GetUserShelf")
            .WithSummary("Another user's shelf, with the same filters as /me/shelf")
            .AllowAnonymous()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<Results<Ok<ShelfPage>, ProblemHttpResult>> HandleAsync(
        string username,
        [AsParameters] ShelfListRequest request,
        ClaimsPrincipal viewer,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await ProfileLookup.FindVisibleAsync(db, username, viewer, cancellationToken);
        if (user is null)
        {
            return ProfileLookup.NotFound.ToProblem();
        }

        var page = await ShelfListQuery.RunAsync(db, user.Id, request, cancellationToken);
        return page.IsSuccess ? TypedResults.Ok(page.Value) : page.Error.ToProblem();
    }
}
