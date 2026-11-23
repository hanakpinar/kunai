using System.Globalization;

using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.Items;

using Microsoft.Net.Http.Headers;

namespace Kunai.Api.Features.Library;

public sealed record ShelfEntryResponse(
    ItemSummary Item,
    ShelfStatus Status,
    int? Rating,
    DateOnly? StartedOn,
    DateOnly? FinishedOn,
    int? Progress,
    string? Review,
    bool IsFavorite,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ShelfEntryResponse From(ShelfEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new ShelfEntryResponse(
            ItemSummary.From(entry.Item),
            entry.Status,
            entry.Rating,
            entry.StartedOn,
            entry.FinishedOn,
            entry.Progress,
            entry.Review,
            entry.IsFavorite,
            entry.CreatedAt,
            entry.UpdatedAt);
    }
}

/// <summary>Strong ETags built from the row version, for If-Match / If-None-Match.</summary>
public static class ShelfEntryETag
{
    public static string For(ShelfEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return $"\"{entry.Version.ToString(CultureInfo.InvariantCulture)}\"";
    }

    public static void Write(HttpResponse response, ShelfEntry entry)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = For(entry);
        response.Headers.CacheControl = "private, no-cache";
    }

    /// <summary>True when any tag in the header is "*" or equals the entry's current tag.</summary>
    public static bool Matches(IList<EntityTagHeaderValue> tags, ShelfEntry entry)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var current = For(entry);
        return tags.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || string.Equals(tag.Tag.Value, current, StringComparison.Ordinal));
    }
}
