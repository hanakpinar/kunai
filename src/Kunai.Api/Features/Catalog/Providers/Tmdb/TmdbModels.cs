namespace Kunai.Api.Features.Catalog.Providers.Tmdb;

/// <summary>The parts of TMDB's JSON that Kunai reads (snake_case on the wire).</summary>
internal static class TmdbModels
{
    internal sealed record SearchResponse(int Page, int TotalPages, IReadOnlyList<SearchResult>? Results);

    internal sealed record SearchResult(
        int Id,
        string? Title,
        string? Name,
        string? OriginalTitle,
        string? OriginalName,
        string? ReleaseDate,
        string? FirstAirDate,
        string? PosterPath);

    internal sealed record Movie(
        int Id,
        string? Title,
        string? OriginalTitle,
        string? ReleaseDate,
        string? Overview,
        string? PosterPath,
        int? Runtime,
        IReadOnlyList<Named>? Genres,
        Credits? Credits);

    internal sealed record Tv(
        int Id,
        string? Name,
        string? OriginalName,
        string? FirstAirDate,
        string? Overview,
        string? PosterPath,
        int? NumberOfEpisodes,
        int? NumberOfSeasons,
        IReadOnlyList<Named>? Genres,
        IReadOnlyList<Named>? CreatedBy);

    internal sealed record Named(string? Name);

    internal sealed record Credits(IReadOnlyList<Crew>? Crew);

    internal sealed record Crew(string? Job, string? Name);
}
