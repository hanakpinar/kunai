using System.ComponentModel.DataAnnotations;

namespace Kunai.Api.Features.Library;

/// <summary>Filters, sort and paging for shelf lists (mine and other users').</summary>
public sealed class ShelfListRequest
{
    /// <summary>book, movie, series, game, anime or manga.</summary>
    [AllowedValues("book", "movie", "series", "game", "anime", "manga", null, ErrorMessage = "type_invalid")]
    public string? Type { get; init; }

    /// <summary>Repeat to match several: status=completed&amp;status=dropped.</summary>
    public string[]? Status { get; init; }

    public bool? Favorite { get; init; }

    /// <summary>Items finished in this year.</summary>
    [Range(1000, 3000, ErrorMessage = "year_out_of_range")]
    public int? Year { get; init; }

    /// <summary>One field, "-" for descending: -updatedAt (default), createdAt, finishedOn, rating.</summary>
    [AllowedValues("-updatedAt", "updatedAt", "-createdAt", "createdAt", "-finishedOn", "finishedOn", "-rating", "rating", null, ErrorMessage = "sort_invalid")]
    public string? Sort { get; init; }

    /// <summary>The nextCursor of the previous page.</summary>
    [StringLength(512, ErrorMessage = "too_long")]
    public string? Cursor { get; init; }

    /// <summary>Page size, 1-50 (default 24).</summary>
    [Range(1, 50, ErrorMessage = "limit_out_of_range")]
    public int? Limit { get; init; }
}
