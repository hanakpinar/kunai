using System.Security.Claims;

using Kunai.Api.Common.Caching;
using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Identity;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

using Npgsql;

namespace Kunai.Api.Features.Library;

public static class UpsertShelfEntry
{
    public const string ConcurrentChange = "concurrent_change";

    private static readonly Error PreconditionFailed =
        Error.PreconditionFailed(ErrorCodes.PreconditionFailed, "The entry changed since you read it, or the precondition does not hold.");

    public static void Map(RouteGroupBuilder shelf) =>
        shelf.MapPut("/{itemId:guid}", HandleAsync)
            .WithName("PutShelfEntry")
            .WithSummary("Add an item to my shelf or replace its entry")
            .WithDescription("201 when created, 200 when replaced. Send If-Match with the ETag to avoid overwriting a change made on another device (412 on mismatch); send If-None-Match: * to only create.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    internal static async Task<Results<Created<ShelfEntryResponse>, Ok<ShelfEntryResponse>, ProblemHttpResult>> HandleAsync(
        Guid itemId,
        ShelfEntryRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        HybridCache cache,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var data = request.ToData();

        var entry = await db.Set<ShelfEntry>().Include(e => e.Item)
            .SingleOrDefaultAsync(e => e.UserId == userId && e.ItemId == itemId, cancellationToken);

        var result = entry is null
            ? await CreateAsync(itemId, userId, data, principal, db, httpContext, cancellationToken)
            : await ReplaceAsync(entry, data, db, httpContext, cancellationToken);

        // Profile, stats and goals are derived from the shelf; recompute them on the next read.
        await cache.RemoveByTagAsync(CacheTags.User(userId), cancellationToken);
        return result;
    }

    private static async Task<Results<Created<ShelfEntryResponse>, Ok<ShelfEntryResponse>, ProblemHttpResult>> CreateAsync(
        Guid itemId,
        Guid userId,
        ShelfEntryData data,
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.GetTypedHeaders().IfMatch.Count > 0)
        {
            return PreconditionFailed.ToProblem();
        }

        var item = await db.Set<Item>().SingleOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null || !GetItem.CanSee(item, principal))
        {
            return Error.NotFound(GetItem.NotFoundCode, "No such item.").ToProblem();
        }

        var created = ShelfEntry.Create(userId, item, data);
        if (!created.IsSuccess)
        {
            return created.Error.ToProblem();
        }

        db.Add(created.Value);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Error.Conflict(ConcurrentChange, "The entry was created by another request at the same time. Read it and try again.").ToProblem();
        }

        ShelfEntryETag.Write(httpContext.Response, created.Value);
        return TypedResults.Created($"/api/v1/me/shelf/{itemId}", ShelfEntryResponse.From(created.Value));
    }

    private static async Task<Results<Created<ShelfEntryResponse>, Ok<ShelfEntryResponse>, ProblemHttpResult>> ReplaceAsync(
        ShelfEntry entry,
        ShelfEntryData data,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var headers = httpContext.Request.GetTypedHeaders();
        if (headers.IfNoneMatch.Count > 0 && ShelfEntryETag.Matches(headers.IfNoneMatch, entry))
        {
            return PreconditionFailed.ToProblem();
        }

        if (headers.IfMatch.Count > 0 && !ShelfEntryETag.Matches(headers.IfMatch, entry))
        {
            return PreconditionFailed.ToProblem();
        }

        var updated = entry.Update(data);
        if (!updated.IsSuccess)
        {
            return updated.Error.ToProblem();
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // With If-Match the client asked for exactly this check; without it, tell it to read and retry.
            return headers.IfMatch.Count > 0
                ? PreconditionFailed.ToProblem()
                : Error.Conflict(ConcurrentChange, "The entry changed at the same time on another request. Read it and try again.").ToProblem();
        }

        ShelfEntryETag.Write(httpContext.Response, entry);
        return TypedResults.Ok(ShelfEntryResponse.From(entry));
    }
}
