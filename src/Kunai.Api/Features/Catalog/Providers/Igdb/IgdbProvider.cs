using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using Kunai.Api.Common.Results;

using Microsoft.Extensions.Options;

using static Kunai.Api.Features.Catalog.Providers.Igdb.IgdbModels;

namespace Kunai.Api.Features.Catalog.Providers.Igdb;

/// <summary>Video games from IGDB, queried with Apicalypse bodies (auth is added by <see cref="IgdbAuthHandler"/>).</summary>
internal sealed partial class IgdbProvider(HttpClient http, IOptions<IgdbOptions> options, ILogger<IgdbProvider> logger) : IMetadataProvider
{
    private const string GameKind = "game";
    private const int PageSize = 20;
    private const int SubtitlePlatforms = 4;

    private const string SearchFields = "name,first_release_date,cover.image_id,platforms.abbreviation,platforms.name";
    private const string DetailFields = "name,first_release_date,summary,cover.image_id,genres.name,platforms.name,"
        + "involved_companies.developer,involved_companies.company.name";

    public ItemSource Source => ItemSource.Igdb;

    private IgdbOptions Settings => options.Value;

    private bool Configured => !string.IsNullOrWhiteSpace(Settings.ClientId) && !string.IsNullOrWhiteSpace(Settings.ClientSecret);

    public bool Supports(ItemType type) => type == ItemType.Game;

    public async Task<Result<ProviderSearchPage>> SearchAsync(ItemType type, string query, int page, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            return ProviderErrors.NotConfigured(Source);
        }

        if (!Supports(type))
        {
            return new ProviderSearchPage([], HasMore: false);
        }

        var offset = (long)(Math.Max(page, 1) - 1) * PageSize;
        var body = $"search \"{Escape(query)}\"; fields {SearchFields}; limit {PageSize}; offset {offset.ToString(CultureInfo.InvariantCulture)};";
        var response = await PostGamesAsync(body, cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        var games = response.Value ?? [];
        var hits = games
            .Select(game => Title(game) is { } title
                ? new SearchHit(
                    Ref(game.Id),
                    ItemType.Game,
                    title,
                    Year(game.FirstReleaseDate),
                    Subtitle(game.Platforms),
                    Cover(game.Cover))
                : null)
            .OfType<SearchHit>()
            .ToList();

        return new ProviderSearchPage(hits, HasMore: games.Count == PageSize);
    }

    public async Task<Result<ItemMetadata>> GetAsync(ExternalRef reference, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            return ProviderErrors.NotConfigured(Source);
        }

        // Only canonical ids, so "igdb:game:042" cannot become a second row next to "igdb:game:42".
        if (!string.Equals(reference.Kind, GameKind, StringComparison.Ordinal)
            || !long.TryParse(reference.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || id <= 0
            || !string.Equals(id.ToString(CultureInfo.InvariantCulture), reference.Id, StringComparison.Ordinal))
        {
            return ProviderErrors.NotFound(reference);
        }

        var response = await PostGamesAsync($"fields {DetailFields}; where id = {reference.Id};", cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not [var game, ..] || game.Id != id || Title(game) is not { } title)
        {
            return ProviderErrors.NotFound(reference);
        }

        var developers = Clean((game.InvolvedCompanies ?? [])
            .Where(c => c.Developer)
            .Select(c => c.Company?.Name));

        return new ItemMetadata(
            Ref(game.Id),
            ItemType.Game,
            title,
            OriginalTitle: null,
            Year(game.FirstReleaseDate),
            PlainText(game.Summary),
            Cover(game.Cover),
            developers,
            Clean((game.Genres ?? []).Select(g => g.Name)),
            ProgressTotal: null)
        {
            Platforms = Clean((game.Platforms ?? []).Select(p => p.Name)),
        };
    }

    private async Task<Result<IReadOnlyList<Game>?>> PostGamesAsync(string apicalypse, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("games", UriKind.Relative))
        {
            Content = new StringContent(apicalypse, Encoding.UTF8, "text/plain"),
        };
        return await ProviderHttp.SendAsync<IReadOnlyList<Game>>(http, request, Source, ProviderHttp.SnakeCase, logger, cancellationToken);
    }

    private ExternalRef Ref(long id) => new(Source, GameKind, id.ToString(CultureInfo.InvariantCulture));

    private Uri? Cover(Image? cover)
    {
        if (string.IsNullOrWhiteSpace(cover?.ImageId))
        {
            return null;
        }

        var url = new Uri(Settings.ImageBaseUrl, $"t_cover_big/{Uri.EscapeDataString(cover.ImageId.Trim())}.jpg");
        return string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            ? new UriBuilder(url) { Scheme = Uri.UriSchemeHttps, Port = url.IsDefaultPort ? -1 : url.Port }.Uri
            : url;
    }

    private static string? Title(Game game) => string.IsNullOrWhiteSpace(game.Name) ? null : game.Name.Trim();

    /// <summary>Platform abbreviations ("PS5, PC"), falling back to the full name when a platform has none.</summary>
    private static string? Subtitle(IReadOnlyList<Platform>? platforms)
    {
        var names = Clean((platforms ?? []).Select(p => string.IsNullOrWhiteSpace(p.Abbreviation) ? p.Name : p.Abbreviation));
        return names.Count == 0 ? null : string.Join(", ", names.Take(SubtitlePlatforms));
    }

    /// <summary>first_release_date is a Unix timestamp in seconds (UTC).</summary>
    private static int? Year(long? releaseDate)
    {
        if (releaseDate is not { } seconds
            || seconds < DateTimeOffset.MinValue.ToUnixTimeSeconds()
            || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return null;
        }

        var year = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.Year;
        return year is >= 1900 and <= 2100 ? year : null;
    }

    private static string Escape(string query) =>
        query.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string? PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = LineBreak().Replace(html, "\n");
        text = HtmlTag().Replace(text, string.Empty);
        // Strip again after decoding, so encoded markup such as "&lt;b&gt;" cannot come back as a tag.
        text = HtmlTag().Replace(WebUtility.HtmlDecode(text), string.Empty);
        text = ExtraLineBreaks().Replace(text, "\n\n").Trim();
        return text.Length == 0 ? null : text;
    }

    private static List<string> Clean(IEnumerable<string?> values) =>
        values
            .Select(v => v?.Trim())
            .OfType<string>()
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    [GeneratedRegex(@"<br\s*/?>|</p\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"</?[a-zA-Z!][^<>]*>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ExtraLineBreaks();
}
