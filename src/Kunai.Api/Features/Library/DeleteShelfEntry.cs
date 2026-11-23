using System.Security.Claims;

using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Library;

public static class DeleteShelfEntry
{
    public static void Map(RouteGroupBuilder shelf) =>
        shelf.MapDelete("/{itemId:guid}", HandleAsync)
            .WithName("DeleteShelfEntry")
            .WithSummary("Remove an item from my shelf")
            .WithDescription("Always 204, also when the item was not on the shelf, so retries are safe.");

    internal static async Task<NoContent> HandleAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        await db.Set<ShelfEntry>().Where(e => e.UserId == userId && e.ItemId == itemId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
