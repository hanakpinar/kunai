using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Kunai.Api.Features.Library;

public static class GetMyShelf
{
    public static void Map(RouteGroupBuilder shelf) =>
        shelf.MapGet("/", HandleAsync)
            .WithName("GetMyShelf")
            .WithSummary("My shelf, filtered and sorted, one page at a time")
            .ProducesValidationProblem();

    internal static async Task<Results<Ok<ShelfPage>, ProblemHttpResult>> HandleAsync(
        [AsParameters] ShelfListRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var page = await ShelfListQuery.RunAsync(db, principal.GetUserId(), request, cancellationToken);
        if (!page.IsSuccess)
        {
            return page.Error.ToProblem();
        }

        httpContext.Response.Headers.CacheControl = "private, no-cache";
        return TypedResults.Ok(page.Value);
    }
}
