using System.Globalization;

using Kunai.Web.Client.Api.Api.V1.Me.Shelf;
using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

namespace Kunai.Web.Client.Pages.Shelf;

/// <summary>
/// The shelf filters as they live in the URL (?type=&amp;status=&amp;favorite=&amp;year=&amp;sort=), so a reload
/// or the back button brings back the same list.
/// </summary>
public sealed record ShelfFilter(ItemType? Type, IReadOnlyList<ShelfStatus> Statuses, bool Favorite, int? Year, string Sort)
{
    public const string DefaultSort = "-updatedAt";

    /// <summary>The six types in tab order.</summary>
    public static IReadOnlyList<ItemType> Types { get; } = [ItemType.Book, ItemType.Movie, ItemType.Series, ItemType.Game, ItemType.Anime, ItemType.Manga];

    public static IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new(DefaultSort, "Son güncellenen"),
        new("-rating", "En yüksek puan"),
        new("-finishedOn", "Bitiş tarihi"),
        new("-createdAt", "Eklenme tarihi"),
    ];

    public static ShelfFilter Empty { get; } = new(null, [], Favorite: false, Year: null, DefaultSort);

    /// <summary>True when something narrows the list; the sort order alone does not.</summary>
    public bool IsFiltered => Type is not null || Statuses.Count > 0 || Favorite || Year is not null;

    /// <summary>Reads the query string leniently: an unknown value is ignored instead of breaking the page.</summary>
    public static ShelfFilter Parse(string? type, IEnumerable<string>? statuses, string? favorite, string? year, string? sort) => new(
        Types.Where(t => string.Equals(CatalogMetadata.QueryValue(t), type, StringComparison.OrdinalIgnoreCase)).Select(t => (ItemType?)t).FirstOrDefault(),
        Ordered((statuses ?? []).Select(ParseStatus).OfType<ShelfStatus>()),
        string.Equals(favorite, "true", StringComparison.OrdinalIgnoreCase),
        int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1000 and <= 3000 ? number : null,
        ParseSort(sort));

    public static string ParseSort(string? sort) =>
        SortOptions.FirstOrDefault(o => string.Equals(o.Value, sort, StringComparison.Ordinal))?.Value ?? DefaultSort;

    /// <summary>The API's wire value for a status, e.g. "inProgress".</summary>
    public static string StatusValue(ShelfStatus status) => status switch
    {
        ShelfStatus.Planned => "planned",
        ShelfStatus.InProgress => "inProgress",
        ShelfStatus.Completed => "completed",
        ShelfStatus.Dropped => "dropped",
        _ => "paused",
    };

    public ShelfFilter Toggle(ShelfStatus status) => this with
    {
        Statuses = Statuses.Contains(status) ? [.. Statuses.Where(s => s != status)] : Ordered(Statuses.Append(status)),
    };

    /// <summary>Drops the statuses a type does not have (a movie is never "in progress").</summary>
    public ShelfFilter KeepOnly(IEnumerable<StatusOption> allowed)
    {
        var statuses = allowed.Select(o => o.Status).ToHashSet();
        return this with { Statuses = [.. Statuses.Where(statuses.Contains)] };
    }

    /// <summary>Query values for <c>NavigationManager.GetUriWithQueryParameters</c>; null removes a key.</summary>
    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["type"] = Type is { } type ? CatalogMetadata.QueryValue(type) : null,
        ["status"] = Statuses.Count > 0 ? Statuses.Select(StatusValue).ToArray() : null,
        ["favorite"] = Favorite ? "true" : null,
        ["year"] = Year?.ToString(CultureInfo.InvariantCulture),
        ["sort"] = string.Equals(Sort, DefaultSort, StringComparison.Ordinal) ? null : Sort,
    };

    /// <summary>Fills GET /me/shelf's query for one page.</summary>
    public void Apply(ShelfRequestBuilder.ShelfRequestBuilderGetQueryParameters query, string? cursor, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Type = Type is { } type ? CatalogMetadata.QueryValue(type) : null;
        query.Status = Statuses.Count > 0 ? [.. Statuses.Select(StatusValue)] : null;
        query.Favorite = Favorite ? true : null;
        query.Year = Year;
        query.Sort = Sort;
        query.Cursor = cursor;
        query.Limit = limit;
    }

    public bool Equals(ShelfFilter? other) =>
        other is not null
        && Type == other.Type
        && Favorite == other.Favorite
        && Year == other.Year
        && string.Equals(Sort, other.Sort, StringComparison.Ordinal)
        && Statuses.SequenceEqual(other.Statuses);

    public override int GetHashCode() => HashCode.Combine(Type, Favorite, Year, StringComparer.Ordinal.GetHashCode(Sort), Statuses.Count);

    private static ShelfStatus? ParseStatus(string value) =>
        Enum.GetValues<ShelfStatus>().Where(s => string.Equals(StatusValue(s), value, StringComparison.OrdinalIgnoreCase)).Select(s => (ShelfStatus?)s).FirstOrDefault();

    // One order regardless of click order, so equal filters give equal URLs.
    private static ShelfStatus[] Ordered(IEnumerable<ShelfStatus> statuses) => [.. statuses.Distinct().Order()];
}

/// <summary>One entry of the sort select: the API's sort value and its Turkish label.</summary>
public sealed record SortOption(string Value, string Label);
