using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Caching;
using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Kunai.Api.Features.Moderation;

/// <summary>Review queue for manually added books. Moderators and admins only.</summary>
public static partial class ModerationEndpoints
{
    public const string NotPending = "item_not_pending";

    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder api)
    {
        var admin = api.MapGroup("/admin/items").WithTags("Moderation").RequireAuthorization(Policies.Moderator);

        admin.MapGet("/pending", GetPendingAsync)
            .WithName("GetPendingItems")
            .WithSummary("Manually added items waiting for review, oldest first")
            .ProducesValidationProblem();

        admin.MapPost("/{id:guid}/approve", (Guid id, ClaimsPrincipal moderator, AppDbContext db, HybridCache cache, TimeProvider time, ILogger<PendingItem> logger, CancellationToken ct) =>
                DecideAsync(id, CatalogStatus.Approved, moderator, db, cache, time, logger, ct))
            .WithName("ApproveItem")
            .WithSummary("Make the item visible to everyone")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{id:guid}/reject", (Guid id, ClaimsPrincipal moderator, AppDbContext db, HybridCache cache, TimeProvider time, ILogger<PendingItem> logger, CancellationToken ct) =>
                DecideAsync(id, CatalogStatus.Rejected, moderator, db, cache, time, logger, ct))
            .WithName("RejectItem")
            .WithSummary("Keep the item hidden (it stays on its creator's shelf)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }

    internal static async Task<Ok<IReadOnlyList<PendingItem>>> GetPendingAsync(
        [Range(1, 100, ErrorMessage = "limit_out_of_range")] int? limit,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var items = await db.Set<Item>().AsNoTracking()
            .Where(i => i.CatalogStatus == CatalogStatus.PendingReview)
            .OrderBy(i => i.CreatedAt)
            .Take(limit ?? 50)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<PendingItem>>(items.Select(i => new PendingItem(ItemResponse.From(i), i.CreatedByUserId, i.CreatedAt)).ToList());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DecideAsync(
        Guid id,
        CatalogStatus decision,
        ClaimsPrincipal moderator,
        AppDbContext db,
        HybridCache cache,
        TimeProvider time,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Conditional update: of two moderators deciding at once, exactly one succeeds.
        var now = time.GetUtcNow();
        var changed = await db.Set<Item>()
            .Where(i => i.Id == id && i.CatalogStatus == CatalogStatus.PendingReview)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.CatalogStatus, decision).SetProperty(i => i.UpdatedAt, now), cancellationToken);

        var item = await db.Set<Item>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Error.NotFound(GetItem.NotFoundCode, "No such item.").ToProblem();
        }

        if (changed == 0)
        {
            return Error.Conflict(NotPending, $"The item is already {item.CatalogStatus}.").ToProblem();
        }

        if (item.CreatedByUserId is { } creator)
        {
            await cache.RemoveByTagAsync(CacheTags.User(creator), cancellationToken);
        }

        var moderatorId = moderator.GetUserId();
        LogDecision(logger, moderatorId, decision, id);
        return TypedResults.NoContent();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Moderator {ModeratorId} set item {ItemId} to {Decision}")]
    private static partial void LogDecision(ILogger logger, Guid moderatorId, CatalogStatus decision, Guid itemId);
}

/// <param name="Item">The item as submitted.</param>
/// <param name="CreatedByUserId">Who added it.</param>
/// <param name="SubmittedAt">When it was added.</param>
public sealed record PendingItem(ItemResponse Item, Guid? CreatedByUserId, DateTimeOffset SubmittedAt);
