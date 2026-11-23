using Kunai.Api.Common.Results;

namespace Kunai.Api.Features.Catalog.Providers;

/// <summary>
/// A source of item metadata (TMDB, Google Books, IGDB...). Implementations translate the provider's API
/// into <see cref="SearchHit"/> and <see cref="ItemMetadata"/>, and turn every failure into an
/// <see cref="Error"/> instead of throwing (see <see cref="ProviderErrors"/>).
/// </summary>
public interface IMetadataProvider
{
    ItemSource Source { get; }

    bool Supports(ItemType type);

    /// <param name="type">One of the types this provider supports.</param>
    /// <param name="query">The user's search text, already trimmed (2-100 characters).</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    Task<Result<ProviderSearchPage>> SearchAsync(ItemType type, string query, int page, CancellationToken cancellationToken);

    /// <summary>Full metadata for one item; <see cref="ProviderErrors.NotFound"/> when the id does not exist.</summary>
    Task<Result<ItemMetadata>> GetAsync(ExternalRef reference, CancellationToken cancellationToken);
}

public sealed record ProviderSearchPage(IReadOnlyList<SearchHit> Hits, bool HasMore);

/// <param name="Ref">Opaque reference to import the item with.</param>
/// <param name="Type">Item type.</param>
/// <param name="Title">Display title (Turkish when the provider has one).</param>
/// <param name="Year">Release or publication year.</param>
/// <param name="Subtitle">A short secondary line: authors, director, studio, platforms...</param>
/// <param name="CoverUrl">HTTPS cover or poster image.</param>
public sealed record SearchHit(ExternalRef Ref, ItemType Type, string Title, int? Year, string? Subtitle, Uri? CoverUrl);

/// <summary>Everything needed to create or refresh a catalog item.</summary>
public sealed record ItemMetadata(
    ExternalRef Ref,
    ItemType Type,
    string Title,
    string? OriginalTitle,
    int? Year,
    string? Description,
    Uri? CoverUrl,
    IReadOnlyList<string> Creators,
    IReadOnlyList<string> Genres,
    int? ProgressTotal)
{
    public string? Isbn13 { get; init; }

    public string? Publisher { get; init; }

    public int? RuntimeMinutes { get; init; }

    public int? SeasonCount { get; init; }

    public IReadOnlyList<string> Platforms { get; init; } = [];

    /// <summary>Anime/manga format such as TV, MOVIE, MANGA, NOVEL.</summary>
    public string? Format { get; init; }

    public int? VolumeCount { get; init; }
}
