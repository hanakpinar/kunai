using System.Security.Claims;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Catalog.Items;

public static class GetItem
{
    public const string NotFoundCode = "item_not_found";

    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet("/items/{id:guid}", HandleAsync)
            .WithTags("Catalog")
            .WithName("GetItem")
            .WithSummary("Item details")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<Results<Ok<ItemResponse>, ProblemHttpResult>> HandleAsync(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await db.Set<Item>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        return item is not null && CanSee(item, principal)
            ? TypedResults.Ok(ItemResponse.From(item))
            : Error.NotFound(NotFoundCode, "No such item.").ToProblem();
    }

    /// <summary>Unreviewed manual items are visible only to the person who added them and to moderators.</summary>
    internal static bool CanSee(Item item, ClaimsPrincipal principal) =>
        item.CatalogStatus == CatalogStatus.Approved
        || principal.IsInRole(Roles.Moderator)
        || (principal.Identity?.IsAuthenticated == true && item.CreatedByUserId == principal.GetUserId());
}
