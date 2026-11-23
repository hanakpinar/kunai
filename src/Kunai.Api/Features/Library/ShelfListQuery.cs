using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;

using Kunai.Api.Common.Results;
using Kunai.Api.Features.Catalog;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Library;

/// <summary>
/// Keyset (cursor) pagination over a user's shelf. The cursor carries the sort key and id of the last row,
/// so pages never repeat or skip entries while the shelf changes. Nullable sort keys sort as their lowest value.
/// </summary>
public static class ShelfListQuery
{
    public const int DefaultLimit = 24;

    public static async Task<Result<ShelfPage>> RunAsync(AppDbContext db, Guid userId, ShelfListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var filtered = Filter(db, userId, request);
        if (!filtered.IsSuccess)
        {
            return filtered.Error;
        }

        var query = filtered.Value;
        var total = await query.CountAsync(cancellationToken);
        var sort = request.Sort ?? "-updatedAt";
        var descending = sort.StartsWith('-');
        var field = sort.TrimStart('-');

        var cursor = Cursor.Decode(request.Cursor, sort);
        if (request.Cursor is not null && cursor is null)
        {
            return Error.Validation(ErrorCodes.InvalidCursor, "The cursor is not valid for this sort.");
        }

        var limit = request.Limit ?? DefaultLimit;
        var page = field switch
        {
            "createdAt" => Page(query, e => e.CreatedAt, descending, cursor, DateTimeOffset.Parse),
            "finishedOn" => Page(query, e => e.FinishedOn ?? DateOnly.MinValue, descending, cursor, v => DateOnly.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture)),
            "rating" => Page(query, e => e.Rating ?? 0, descending, cursor, v => int.Parse(v, CultureInfo.InvariantCulture)),
            _ => Page(query, e => e.UpdatedAt, descending, cursor, DateTimeOffset.Parse),
        };

        var rows = await page.Include(e => e.Item).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).ToList();
        var next = hasMore ? Cursor.Encode(sort, SortValue(items[^1], field), items[^1].Id) : null;

        return new ShelfPage(items.Select(ShelfEntryResponse.From).ToList(), next, total);
    }

    private static Result<IQueryable<ShelfEntry>> Filter(AppDbContext db, Guid userId, ShelfListRequest request)
    {
        var statuses = new List<ShelfStatus>();
        foreach (var value in request.Status ?? [])
        {
            if (!Enum.TryParse<ShelfStatus>(value, ignoreCase: true, out var status) || !Enum.IsDefined(status))
            {
                return Error.ValidationField("status", "status_invalid", $"Unknown status '{value}'.");
            }

            statuses.Add(status);
        }

        var query = db.Set<ShelfEntry>().AsNoTracking().Where(e => e.UserId == userId);
        if (request.Type is not null)
        {
            var type = Enum.Parse<ItemType>(request.Type, ignoreCase: true);
            query = query.Where(e => e.Item.Type == type);
        }

        if (statuses.Count > 0)
        {
            query = query.Where(e => statuses.Contains(e.Status));
        }

        if (request.Favorite is { } favorite)
        {
            query = query.Where(e => e.IsFavorite == favorite);
        }

        if (request.Year is { } year)
        {
            var from = new DateOnly(year, 1, 1);
            var to = new DateOnly(year, 12, 31);
            query = query.Where(e => e.FinishedOn >= from && e.FinishedOn <= to);
        }

        return Result.Ok(query);
    }

    private static IQueryable<ShelfEntry> Page<TKey>(
        IQueryable<ShelfEntry> query,
        Expression<Func<ShelfEntry, TKey>> key,
        bool descending,
        Cursor? after,
        Func<string, TKey> parse)
    {
        if (after is not null)
        {
            var value = Expression.Constant(parse(after.Value), typeof(TKey));
            var id = Expression.Constant(after.Id);
            var entry = key.Parameters[0];
            var keyBody = key.Body;
            var idCompare = Expression.Call(Expression.Property(entry, nameof(ShelfEntry.Id)), typeof(Guid).GetMethod(nameof(Guid.CompareTo), [typeof(Guid)])!, id);
            var zero = Expression.Constant(0);

            // (key < after) OR (key == after AND id < afterId), mirrored for ascending order.
            var beyondKey = descending ? Expression.LessThan(keyBody, value) : Expression.GreaterThan(keyBody, value);
            var sameKey = Expression.Equal(keyBody, value);
            var beyondId = descending ? Expression.LessThan(idCompare, zero) : Expression.GreaterThan(idCompare, zero);
            var predicate = Expression.OrElse(beyondKey, Expression.AndAlso(sameKey, beyondId));
            query = query.Where(Expression.Lambda<Func<ShelfEntry, bool>>(predicate, entry));
        }

        return descending
            ? query.OrderByDescending(key).ThenByDescending(e => e.Id)
            : query.OrderBy(key).ThenBy(e => e.Id);
    }

    private static string SortValue(ShelfEntry entry, string field) => field switch
    {
        "createdAt" => entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        "finishedOn" => (entry.FinishedOn ?? DateOnly.MinValue).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "rating" => (entry.Rating ?? 0).ToString(CultureInfo.InvariantCulture),
        _ => entry.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
    };

    private sealed record Cursor(string Sort, string Value, Guid Id)
    {
        public static string Encode(string sort, string value, Guid id) =>
            WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Cursor(sort, value, id)));

        public static Cursor? Decode(string? encoded, string sort)
        {
            if (encoded is null)
            {
                return null;
            }

            try
            {
                var cursor = JsonSerializer.Deserialize<Cursor>(WebEncoders.Base64UrlDecode(encoded));
                return cursor is not null && string.Equals(cursor.Sort, sort, StringComparison.Ordinal) ? cursor : null;
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            {
                return null;
            }
        }
    }
}

/// <param name="Items">The entries on this page.</param>
/// <param name="NextCursor">Pass as "cursor" for the next page; null on the last page.</param>
/// <param name="TotalCount">How many entries match the filters in total.</param>
public sealed record ShelfPage(IReadOnlyList<ShelfEntryResponse> Items, string? NextCursor, int TotalCount);
