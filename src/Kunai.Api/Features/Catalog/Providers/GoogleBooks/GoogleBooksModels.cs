namespace Kunai.Api.Features.Catalog.Providers.GoogleBooks;

/// <summary>The parts of Google Books' JSON that Kunai reads (camelCase on the wire).</summary>
internal static class GoogleBooksModels
{
    /// <summary>A volumes search; "items" is absent when nothing matches.</summary>
    internal sealed record VolumeList(int TotalItems, IReadOnlyList<Volume>? Items);

    internal sealed record Volume(string? Id, VolumeInfo? VolumeInfo);

    internal sealed record VolumeInfo(
        string? Title,
        string? Subtitle,
        IReadOnlyList<string?>? Authors,
        string? Publisher,
        string? PublishedDate,
        string? Description,
        IReadOnlyList<IndustryIdentifier>? IndustryIdentifiers,
        int? PageCount,
        IReadOnlyList<string?>? Categories,
        ImageLinks? ImageLinks);

    /// <summary>Type is ISBN_10, ISBN_13, ISSN or OTHER.</summary>
    internal sealed record IndustryIdentifier(string? Type, string? Identifier);

    internal sealed record ImageLinks(
        string? SmallThumbnail,
        string? Thumbnail,
        string? Small,
        string? Medium,
        string? Large,
        string? ExtraLarge);
}
