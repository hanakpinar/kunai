using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Library;

public static class GetShelfEntry
{
    public const string NotFoundCode = "shelf_entry_not_found";

    public static void Map(RouteGroupBuilder shelf) =>
        shelf.MapGet("/{itemId:guid}", HandleAsync)
            .WithName("GetShelfEntry")
            .WithSummary("My entry for an item, with its ETag")
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<Results<Ok<ShelfEntryResponse>, StatusCodeHttpResult, ProblemHttpResult>> HandleAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var entry = await db.Set<ShelfEntry>().AsNoTracking().Include(e => e.Item)
            .SingleOrDefaultAsync(e => e.UserId == userId && e.ItemId == itemId, cancellationToken);
        if (entry is null)
        {
            return Error.NotFound(NotFoundCode, "This item is not on your shelf.").ToProblem();
        }

        ShelfEntryETag.Write(httpContext.Response, entry);
        var ifNoneMatch = httpContext.Request.GetTypedHeaders().IfNoneMatch;
        return ifNoneMatch.Count > 0 && ShelfEntryETag.Matches(ifNoneMatch, entry)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(ShelfEntryResponse.From(entry));
    }
}
