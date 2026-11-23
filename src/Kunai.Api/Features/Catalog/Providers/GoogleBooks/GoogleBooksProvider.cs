using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using Kunai.Api.Common.Results;

using Microsoft.Extensions.Options;

using static Kunai.Api.Features.Catalog.Providers.GoogleBooks.GoogleBooksModels;

namespace Kunai.Api.Features.Catalog.Providers.GoogleBooks;

/// <summary>Books from Google Books. Works without an API key; a key only raises the daily quota.</summary>
internal sealed partial class GoogleBooksProvider(HttpClient http, IOptions<GoogleBooksOptions> options, ILogger<GoogleBooksProvider> logger) : IMetadataProvider
{
    private const string VolumeKind = "volume";
    private const int PageSize = 20;

    public ItemSource Source => ItemSource.GoogleBooks;

    private GoogleBooksOptions Settings => options.Value;

    public bool Supports(ItemType type) => type == ItemType.Book;

    public async Task<Result<ProviderSearchPage>> SearchAsync(ItemType type, string query, int page, CancellationToken cancellationToken)
    {
        var startIndex = (page - 1) * PageSize;
        var url = $"volumes?q={Uri.EscapeDataString(query)}&startIndex={startIndex.ToString(CultureInfo.InvariantCulture)}"
            + $"&maxResults={PageSize.ToString(CultureInfo.InvariantCulture)}&printType=books{KeyParameter('&')}";
        var response = await SendAsync<VolumeList>(url, cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { } body)
        {
            return new ProviderSearchPage([], HasMore: false);
        }

        var volumes = body.Items ?? [];
        var hits = volumes
            .Select(volume => volume is { Id: { } id, VolumeInfo: { } info } && IsVolumeId(id) && FullTitle(info) is { } title
                ? new SearchHit(
                    new ExternalRef(Source, VolumeKind, id),
                    ItemType.Book,
                    title,
                    Year(info.PublishedDate),
                    Subtitle(info.Authors),
                    Cover(info.ImageLinks))
                : null)
            .OfType<SearchHit>()
            .DistinctBy(hit => hit.Ref.Id, StringComparer.Ordinal)
            .ToList();

        // totalItems is only an estimate, so an empty page ends the results even when it promises more.
        var hasMore = volumes.Count > 0 && startIndex + volumes.Count < body.TotalItems;
        return new ProviderSearchPage(hits, hasMore);
    }

    public async Task<Result<ItemMetadata>> GetAsync(ExternalRef reference, CancellationToken cancellationToken)
    {
        if (!string.Equals(reference.Kind, VolumeKind, StringComparison.Ordinal) || !IsVolumeId(reference.Id))
        {
            return ProviderErrors.NotFound(reference);
        }

        var response = await SendAsync<Volume>($"volumes/{reference.Id}{KeyParameter('?')}", cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { VolumeInfo: { } info } || FullTitle(info) is not { } title)
        {
            return ProviderErrors.NotFound(reference);
        }

        return new ItemMetadata(
            reference,
            ItemType.Book,
            title,
            OriginalTitle: null,
            Year(info.PublishedDate),
            PlainText(info.Description),
            Cover(info.ImageLinks),
            Clean(info.Authors),
            Clean(info.Categories),
            info.PageCount is > 0 ? info.PageCount : null)
        {
            Isbn13 = Isbn13(info.IndustryIdentifiers),
            Publisher = string.IsNullOrWhiteSpace(info.Publisher) ? null : info.Publisher.Trim(),
        };
    }

    private async Task<Result<T?>> SendAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        return await ProviderHttp.SendAsync<T>(http, request, Source, ProviderHttp.CamelCase, logger, cancellationToken);
    }

    private string KeyParameter(char separator) =>
        string.IsNullOrWhiteSpace(Settings.ApiKey) ? string.Empty : $"{separator}key={Uri.EscapeDataString(Settings.ApiKey.Trim())}";

    /// <summary>Volume ids are short URL-safe strings such as "zyTCAlFPjgYC"; anything else cannot exist.</summary>
    private static bool IsVolumeId(string id) =>
        id.Length is > 0 and <= 40 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string? FullTitle(VolumeInfo info)
    {
        var title = info.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        var subtitle = info.Subtitle?.Trim();
        return string.IsNullOrEmpty(subtitle) ? title : $"{title}: {subtitle}";
    }

    private static string? Subtitle(IReadOnlyList<string?>? authors) =>
        Clean(authors) is { Count: > 0 } names ? string.Join(", ", names) : null;

    private static int? Year(string? date) =>
        date is { Length: >= 4 }
        && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
        && year is >= 1000 and <= 2999
            ? year
            : null;

    /// <summary>Google serves covers over http with a page-curl effect; ask for the plain https image.</summary>
    private static Uri? Cover(ImageLinks? links)
    {
        var url = links is null
            ? null
            : new[] { links.Thumbnail, links.SmallThumbnail, links.Small, links.Medium, links.Large, links.ExtraLarge }
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        if (url is null)
        {
            return null;
        }

        url = url.Trim().Replace("&edge=curl", string.Empty, StringComparison.Ordinal);
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url["http://".Length..];
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri is { Scheme: "https" } ? uri : null;
    }

    /// <summary>Descriptions of full volumes are HTML (&lt;p&gt;, &lt;br&gt;, &lt;b&gt;...); keep only the text and line breaks.</summary>
    private static string? PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = LineBreakTag().Replace(html, "\n");
        text = Tag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);

        // Encoded markup such as "&lt;b&gt;" only turns into a tag after decoding.
        text = Tag().Replace(text, string.Empty);

        var lines = text.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Select(line => Whitespace().Replace(line, " ").Trim());
        text = BlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
        return text.Length == 0 ? null : text;
    }

    private static string? Isbn13(IReadOnlyList<IndustryIdentifier>? identifiers) =>
        (identifiers ?? [])
            .Where(identifier => string.Equals(identifier.Type, "ISBN_13", StringComparison.Ordinal))
            .Select(identifier => identifier.Identifier?.Replace("-", string.Empty, StringComparison.Ordinal).Trim())
            .FirstOrDefault(isbn => isbn is { Length: 13 } && isbn.All(char.IsAsciiDigit));

    private static List<string> Clean(IReadOnlyList<string?>? values) =>
        (values ?? [])
            .Select(value => value?.Trim())
            .OfType<string>()
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    [GeneratedRegex(@"<\s*(?:br|/p|/div|/li|/h[1-6])\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex LineBreakTag();

    [GeneratedRegex("</?[A-Za-z!][^>]*>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex BlankLines();
}
