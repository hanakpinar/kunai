namespace Kunai.Api.Features.Catalog.Providers.AniList;

/// <summary>The GraphQL request and the parts of AniList's JSON that Kunai reads (camelCase on the wire).</summary>
internal static class AniListModels
{
    internal sealed record GraphQlRequest<TVariables>(string Query, TVariables Variables);

    internal sealed record SearchVariables(string Search, string Type, int Page, int PerPage);

    internal sealed record MediaVariables(int Id, string Type);

    internal sealed record GraphQlResponse<TData>(TData? Data, IReadOnlyList<GraphQlError?>? Errors);

    internal sealed record GraphQlError(string? Message);

    internal sealed record PageData(Page? Page);

    internal sealed record Page(PageInfo? PageInfo, IReadOnlyList<Media?>? Media);

    internal sealed record PageInfo(bool HasNextPage);

    internal sealed record MediaData(Media? Media);

    internal sealed record Media(
        int Id,
        Title? Title,
        FuzzyDate? StartDate,
        CoverImage? CoverImage,
        string? Description,
        IReadOnlyList<string?>? Genres,
        string? Format,
        int? Episodes,
        int? Chapters,
        int? Volumes,
        bool? IsAdult,
        Connection<Studio>? Studios,
        Connection<Staff>? Staff);

    internal sealed record Title(string? Romaji, string? English, string? Native);

    internal sealed record FuzzyDate(int? Year);

    internal sealed record CoverImage(string? ExtraLarge, string? Large);

    internal sealed record Connection<TNode>(IReadOnlyList<TNode?>? Nodes);

    internal sealed record Studio(string? Name);

    internal sealed record Staff(StaffName? Name);

    internal sealed record StaffName(string? Full);
}
