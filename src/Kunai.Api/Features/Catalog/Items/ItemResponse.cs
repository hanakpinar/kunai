namespace Kunai.Api.Features.Catalog.Items;

/// <param name="Id">Kunai item id.</param>
/// <param name="Type">Item type.</param>
/// <param name="Title">Display title.</param>
/// <param name="OriginalTitle">Original-language title, when different.</param>
/// <param name="Year">Release or publication year.</param>
/// <param name="Description">Plain-text synopsis.</param>
/// <param name="CoverUrl">HTTPS cover image, hosted by the provider.</param>
/// <param name="Creators">Authors, directors, developers or studios.</param>
/// <param name="Genres">Genres as the provider names them.</param>
/// <param name="ProgressTotal">Pages, episodes or chapters, when known.</param>
/// <param name="Source">Metadata provider; show its attribution (see /item-types).</param>
/// <param name="Ref">Provider reference such as "tmdb:movie:603"; null for manual items.</param>
/// <param name="CatalogStatus">approved, or pendingReview/rejected for manual items.</param>
/// <param name="Details">Type-specific fields; the ones that do not apply are null.</param>
public sealed record ItemResponse(
    Guid Id,
    ItemType Type,
    string Title,
    string? OriginalTitle,
    int? Year,
    string? Description,
    string? CoverUrl,
    IReadOnlyList<string> Creators,
    IReadOnlyList<string> Genres,
    int? ProgressTotal,
    ItemSource Source,
    string? Ref,
    CatalogStatus CatalogStatus,
    ItemDetails Details)
{
    public static ItemResponse From(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemResponse(
            item.Id,
            item.Type,
            item.Title,
            item.OriginalTitle,
            item.Year,
            item.Description,
            item.CoverUrl,
            item.Creators.ToList(),
            item.Genres.ToList(),
            item.ProgressTotal,
            item.Source,
            ExternalRef.FromStored(item.Source, item.ExternalId)?.ToString(),
            item.CatalogStatus,
            new ItemDetails(
                (item as Book)?.Isbn13,
                (item as Book)?.Publisher,
                (item as Movie)?.RuntimeMinutes,
                (item as Series)?.SeasonCount,
                (item as Game)?.Platforms.ToList() ?? [],
                (item as Anime)?.Format ?? (item as Manga)?.Format,
                (item as Manga)?.VolumeCount));
    }
}

public sealed record ItemDetails(
    string? Isbn13,
    string? Publisher,
    int? RuntimeMinutes,
    int? SeasonCount,
    IReadOnlyList<string> Platforms,
    string? Format,
    int? VolumeCount);
