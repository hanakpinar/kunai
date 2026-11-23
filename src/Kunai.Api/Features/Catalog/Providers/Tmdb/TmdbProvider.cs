using System.Globalization;

using Kunai.Api.Common.Results;

using Microsoft.Extensions.Options;

using static Kunai.Api.Features.Catalog.Providers.Tmdb.TmdbModels;

namespace Kunai.Api.Features.Catalog.Providers.Tmdb;

/// <summary>Movies and TV series from TMDB, in Turkish when available (falls back to English descriptions).</summary>
internal sealed class TmdbProvider(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbProvider> logger) : IMetadataProvider
{
    private const string MovieKind = "movie";
    private const string TvKind = "tv";

    public ItemSource Source => ItemSource.Tmdb;

    private TmdbOptions Settings => options.Value;

    private bool Configured => !string.IsNullOrWhiteSpace(Settings.AccessToken);

    public bool Supports(ItemType type) => type is ItemType.Movie or ItemType.Series;

    public async Task<Result<ProviderSearchPage>> SearchAsync(ItemType type, string query, int page, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            return ProviderErrors.NotConfigured(Source);
        }

        var kind = type == ItemType.Movie ? MovieKind : TvKind;
        var url = $"search/{kind}?query={Uri.EscapeDataString(query)}&page={page.ToString(CultureInfo.InvariantCulture)}&include_adult=false&language={Settings.Language}";
        var response = await SendAsync<SearchResponse>(url, cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { } body)
        {
            return new ProviderSearchPage([], HasMore: false);
        }

        var hits = (body.Results ?? [])
            .Select(result =>
            {
                var title = result.Title ?? result.Name;
                var original = result.OriginalTitle ?? result.OriginalName;
                return title is null
                    ? null
                    : new SearchHit(
                        new ExternalRef(Source, kind, result.Id.ToString(CultureInfo.InvariantCulture)),
                        type,
                        title,
                        Year(result.ReleaseDate ?? result.FirstAirDate),
                        string.Equals(original, title, StringComparison.Ordinal) ? null : original,
                        Image(result.PosterPath));
            })
            .OfType<SearchHit>()
            .ToList();

        return new ProviderSearchPage(hits, body.Page < body.TotalPages);
    }

    public async Task<Result<ItemMetadata>> GetAsync(ExternalRef reference, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            return ProviderErrors.NotConfigured(Source);
        }

        if (!int.TryParse(reference.Id, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return ProviderErrors.NotFound(reference);
        }

        return reference.Kind switch
        {
            MovieKind => await GetMovieAsync(reference, cancellationToken),
            TvKind => await GetTvAsync(reference, cancellationToken),
            _ => ProviderErrors.NotFound(reference),
        };
    }

    private async Task<Result<ItemMetadata>> GetMovieAsync(ExternalRef reference, CancellationToken cancellationToken)
    {
        var response = await SendAsync<Movie>($"movie/{reference.Id}?language={Settings.Language}&append_to_response=credits", cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { Title: not null } movie)
        {
            return ProviderErrors.NotFound(reference);
        }

        var overview = await OverviewAsync(movie.Overview, $"movie/{reference.Id}", cancellationToken);
        var directors = (movie.Credits?.Crew ?? [])
            .Where(c => string.Equals(c.Job, "Director", StringComparison.Ordinal) && c.Name is not null)
            .Select(c => c.Name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ItemMetadata(
            reference,
            ItemType.Movie,
            movie.Title,
            movie.OriginalTitle,
            Year(movie.ReleaseDate),
            overview,
            Image(movie.PosterPath),
            directors,
            Names(movie.Genres),
            ProgressTotal: null)
        {
            RuntimeMinutes = movie.Runtime is > 0 ? movie.Runtime : null,
        };
    }

    private async Task<Result<ItemMetadata>> GetTvAsync(ExternalRef reference, CancellationToken cancellationToken)
    {
        var response = await SendAsync<Tv>($"tv/{reference.Id}?language={Settings.Language}", cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { Name: not null } tv)
        {
            return ProviderErrors.NotFound(reference);
        }

        var overview = await OverviewAsync(tv.Overview, $"tv/{reference.Id}", cancellationToken);

        return new ItemMetadata(
            reference,
            ItemType.Series,
            tv.Name,
            tv.OriginalName,
            Year(tv.FirstAirDate),
            overview,
            Image(tv.PosterPath),
            Names(tv.CreatedBy),
            Names(tv.Genres),
            tv.NumberOfEpisodes is > 0 ? tv.NumberOfEpisodes : null)
        {
            SeasonCount = tv.NumberOfSeasons is > 0 ? tv.NumberOfSeasons : null,
        };
    }

    /// <summary>Many titles have no Turkish overview; use the English one then.</summary>
    private async Task<string?> OverviewAsync(string? localized, string path, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        var fallback = await SendAsync<Movie>($"{path}?language={Settings.FallbackLanguage}", cancellationToken);
        return fallback is { IsSuccess: true, Value.Overview: { Length: > 0 } overview } ? overview : null;
    }

    private async Task<Result<T?>> SendAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        return await ProviderHttp.SendAsync<T>(http, request, Source, ProviderHttp.SnakeCase, logger, cancellationToken);
    }

    private Uri? Image(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : new Uri(Settings.ImageBaseUrl, path.TrimStart('/'));

    private static int? Year(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            ? year
            : null;

    private static List<string> Names(IReadOnlyList<Named>? values) =>
        (values ?? []).Select(v => v.Name).OfType<string>().Where(n => n.Length > 0).Distinct(StringComparer.Ordinal).ToList();
}
