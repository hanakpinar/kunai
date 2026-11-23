using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Kunai.Api.Common.Results;

using static Kunai.Api.Features.Catalog.Providers.AniList.AniListModels;

namespace Kunai.Api.Features.Catalog.Providers.AniList;

/// <summary>Anime and manga from AniList's GraphQL API (no key needed, adult titles excluded).</summary>
internal sealed partial class AniListProvider(HttpClient http, ILogger<AniListProvider> logger) : IMetadataProvider
{
    private const string AnimeKind = "anime";
    private const string MangaKind = "manga";
    private const int PageSize = 20;

    private const string SearchQuery = """
        query ($search: String, $type: MediaType, $page: Int, $perPage: Int) {
          Page(page: $page, perPage: $perPage) {
            pageInfo { hasNextPage }
            media(search: $search, type: $type, isAdult: false, sort: SEARCH_MATCH) {
              id
              title { romaji english native }
              startDate { year }
              coverImage { large }
              format
              episodes
              chapters
              studios(isMain: true) { nodes { name } }
              staff(perPage: 3, sort: RELEVANCE) { nodes { name { full } } }
            }
          }
        }
        """;

    private const string MediaQuery = """
        query ($id: Int, $type: MediaType) {
          Media(id: $id, type: $type) {
            id
            title { romaji english native }
            startDate { year }
            coverImage { extraLarge large }
            description(asHtml: false)
            genres
            format
            episodes
            chapters
            volumes
            isAdult
            studios(isMain: true) { nodes { name } }
            staff(perPage: 4, sort: RELEVANCE) { nodes { name { full } } }
          }
        }
        """;

    public ItemSource Source => ItemSource.AniList;

    public bool Supports(ItemType type) => type is ItemType.Anime or ItemType.Manga;

    public async Task<Result<ProviderSearchPage>> SearchAsync(ItemType type, string query, int page, CancellationToken cancellationToken)
    {
        var variables = new SearchVariables(query, MediaType(type), page, PageSize);
        var response = await SendAsync<PageData, SearchVariables>(SearchQuery, variables, cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { } body)
        {
            return new ProviderSearchPage([], HasMore: false);
        }

        if (body.Data?.Page is not { } result)
        {
            return Failed(body.Errors);
        }

        var kind = type == ItemType.Anime ? AnimeKind : MangaKind;
        var hits = (result.Media ?? [])
            .OfType<Media>()
            .Select(media =>
            {
                var names = type == ItemType.Anime ? Studios(media) : StaffNames(media);
                return DisplayTitle(media.Title) is not { } title
                    ? null
                    : new SearchHit(
                        new ExternalRef(Source, kind, media.Id.ToString(CultureInfo.InvariantCulture)),
                        type,
                        title,
                        Year(media.StartDate),
                        names.Count == 0 ? null : string.Join(", ", names.Take(2)),
                        Cover(media.CoverImage?.Large));
            })
            .OfType<SearchHit>()
            .ToList();

        return new ProviderSearchPage(hits, result.PageInfo?.HasNextPage == true);
    }

    public async Task<Result<ItemMetadata>> GetAsync(ExternalRef reference, CancellationToken cancellationToken)
    {
        ItemType? type = reference.Kind switch
        {
            AnimeKind => ItemType.Anime,
            MangaKind => ItemType.Manga,
            _ => null,
        };

        // Only canonical ids, so "anime:021" cannot become a second catalog row for "anime:21".
        if (type is not { } itemType
            || !int.TryParse(reference.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || !string.Equals(id.ToString(CultureInfo.InvariantCulture), reference.Id, StringComparison.Ordinal))
        {
            return ProviderErrors.NotFound(reference);
        }

        var response = await SendAsync<MediaData, MediaVariables>(MediaQuery, new MediaVariables(id, MediaType(itemType)), cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        // AniList answers an unknown id with HTTP 404 (a null body here); a null Media means the same.
        if (response.Value is not { } body)
        {
            return ProviderErrors.NotFound(reference);
        }

        if (body.Data is null)
        {
            return Failed(body.Errors);
        }

        // Adult titles are excluded from search; a hand-made ref must not bring them into the shared catalog either.
        if (body.Data.Media is not { IsAdult: not true } media || DisplayTitle(media.Title) is not { } title)
        {
            return ProviderErrors.NotFound(reference);
        }

        var isAnime = itemType == ItemType.Anime;
        return new ItemMetadata(
            reference,
            itemType,
            title,
            OriginalTitle(media.Title, title),
            Year(media.StartDate),
            Description(media.Description),
            Cover(media.CoverImage?.ExtraLarge) ?? Cover(media.CoverImage?.Large),
            isAnime ? Studios(media) : StaffNames(media),
            Distinct(media.Genres),
            Positive(isAnime ? media.Episodes : media.Chapters))
        {
            Format = Text(media.Format),
            VolumeCount = isAnime ? null : Positive(media.Volumes),
        };
    }

    private async Task<Result<GraphQlResponse<TData>?>> SendAsync<TData, TVariables>(string query, TVariables variables, CancellationToken cancellationToken)
        where TData : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(string.Empty, UriKind.Relative))
        {
            Content = JsonContent.Create(new GraphQlRequest<TVariables>(query, variables), options: ProviderHttp.CamelCase),
        };
        return await ProviderHttp.SendAsync<GraphQlResponse<TData>>(http, request, Source, ProviderHttp.CamelCase, logger, cancellationToken);
    }

    /// <summary>A 200 without data carries GraphQL errors; report it as an outage so it is not cached as "no results".</summary>
    private Error Failed(IReadOnlyList<GraphQlError?>? errors)
    {
        LogGraphQlErrors(logger, string.Join("; ", (errors ?? []).Select(e => e?.Message).OfType<string>()));
        return ProviderErrors.Unavailable(Source);
    }

    private static string MediaType(ItemType type) => type == ItemType.Anime ? "ANIME" : "MANGA";

    private static string? DisplayTitle(Title? title) => Text(title?.English) ?? Text(title?.Romaji) ?? Text(title?.Native);

    private static string? OriginalTitle(Title? title, string displayTitle)
    {
        var original = Text(title?.Native) ?? Text(title?.Romaji);
        return string.Equals(original, displayTitle, StringComparison.Ordinal) ? null : original;
    }

    private static int? Year(FuzzyDate? date) => date?.Year is >= 1000 and <= 3000 ? date.Year : null;

    private static int? Positive(int? value) => value is > 0 ? value : null;

    /// <summary>AniList serves https covers today; older or mirrored URLs may still be http.</summary>
    private static Uri? Cover(string? value)
    {
        if (!Uri.TryCreate(Text(value), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ? uri : null;
    }

    /// <summary>Descriptions mix HTML (br, i, b) and entities even with asHtml:false; keep plain text with paragraph breaks.</summary>
    private static string? Description(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var text = Tag().Replace(LineBreak().Replace(value, "\n"), string.Empty);

            // Decoding can reveal escaped tags such as "&lt;i&gt;", so strip once more afterwards.
            text = Tag().Replace(WebUtility.HtmlDecode(text), string.Empty);
            var lines = text.Split('\n').Select(line => Spaces().Replace(line, " ").Trim());
            text = BlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
            return text.Length == 0 ? null : text;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static List<string> Studios(Media media) => Distinct((media.Studios?.Nodes ?? []).Select(studio => studio?.Name));

    private static List<string> StaffNames(Media media) => Distinct((media.Staff?.Nodes ?? []).Select(staff => staff?.Name?.Full));

    private static List<string> Distinct(IEnumerable<string?>? values) =>
        (values ?? []).Select(Text).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"<br\s*/?>\r?\n?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex Tag();

    [GeneratedRegex(@"[^\S\n]+", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n{3,}", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex BlankLines();

    [LoggerMessage(Level = LogLevel.Warning, Message = "AniList answered without data: {Errors}")]
    private static partial void LogGraphQlErrors(ILogger logger, string errors);
}
