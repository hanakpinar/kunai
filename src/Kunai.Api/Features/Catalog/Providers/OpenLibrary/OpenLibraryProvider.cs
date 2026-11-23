using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

using Kunai.Api.Common.Results;

using Microsoft.Extensions.Options;

using static Kunai.Api.Features.Catalog.Providers.OpenLibrary.OpenLibraryModels;

namespace Kunai.Api.Features.Catalog.Providers.OpenLibrary;

/// <summary>Books from Open Library: keyless, and the fallback when Google Books fails.</summary>
internal sealed partial class OpenLibraryProvider(HttpClient http, IOptions<OpenLibraryOptions> options, ILogger<OpenLibraryProvider> logger) : IMetadataProvider
{
    private const string WorkKind = "work";
    private const string WorksPrefix = "/works/";
    private const string AuthorsPrefix = "/authors/";
    private const string SearchFields = "key,title,author_name,first_publish_year,cover_i,number_of_pages_median";
    private const int PageSize = 20;
    private const int MaxAuthors = 3;
    private const int MaxGenres = 10;
    private const int MaxDescriptionInput = 20_000;

    private static readonly SearchValues<char> TagPrefixChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789_-");

    public ItemSource Source => ItemSource.OpenLibrary;

    private OpenLibraryOptions Settings => options.Value;

    public bool Supports(ItemType type) => type == ItemType.Book;

    public async Task<Result<ProviderSearchPage>> SearchAsync(ItemType type, string query, int page, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"search.json?q={Uri.EscapeDataString(query)}&page={page}&limit={PageSize}&fields={SearchFields}");
        var response = await SendAsync<SearchResponse>(url, cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { } body)
        {
            return new ProviderSearchPage([], HasMore: false);
        }

        var hits = (body.Docs ?? []).Select(ToHit).OfType<SearchHit>().ToList();
        var total = body.NumFound ?? body.NumFoundLegacy ?? 0;
        return new ProviderSearchPage(hits, page * PageSize < total);
    }

    public async Task<Result<ItemMetadata>> GetAsync(ExternalRef reference, CancellationToken cancellationToken)
    {
        if (!string.Equals(reference.Kind, WorkKind, StringComparison.Ordinal) || !IsOlid(reference.Id, 'W'))
        {
            return ProviderErrors.NotFound(reference);
        }

        var response = await GetWorkAsync(reference.Id, cancellationToken);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        if (response.Value is not { } work || work.Title?.Trim() is not { Length: > 0 } title)
        {
            return ProviderErrors.NotFound(reference);
        }

        var authorIds = (work.Authors ?? [])
            .Select(a => OlId(KeyOf(a.Author), AuthorsPrefix, 'A'))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MaxAuthors);
        var authors = await Task.WhenAll(authorIds.Select(id => SendAsync<Author>($"authors/{id}.json", cancellationToken)));
        if (authors.FirstOrDefault(a => !a.IsSuccess) is { Error: { } error })
        {
            return error;
        }

        return new ItemMetadata(
            reference,
            ItemType.Book,
            title,
            OriginalTitle: null,
            YearOf(work.FirstPublishDate),
            Description(work.Description),
            Cover((work.Covers ?? []).FirstOrDefault(id => id > 0)),
            Clean(authors.Select(a => a.Value?.Name ?? a.Value?.PersonalName)).ToList(),
            Clean(work.Subjects).Where(s => !IsMachineTag(s)).Take(MaxGenres).ToList(),
            ProgressTotal: null);
    }

    private SearchHit? ToHit(SearchDoc doc)
    {
        var id = OlId(doc.Key, WorksPrefix, 'W');
        var title = doc.Title?.Trim();
        if (id is null || string.IsNullOrEmpty(title))
        {
            return null;
        }

        var authors = Clean(doc.AuthorName).Take(MaxAuthors).ToList();
        return new SearchHit(
            new ExternalRef(Source, WorkKind, id),
            ItemType.Book,
            title,
            PlausibleYear(doc.FirstPublishYear),
            authors.Count > 0 ? string.Join(", ", authors) : null,
            Cover(doc.CoverI));
    }

    /// <summary>Merged works answer with a redirect record instead of HTTP 301; follow it once.</summary>
    private async Task<Result<Work?>> GetWorkAsync(string id, CancellationToken cancellationToken)
    {
        var response = await SendAsync<Work>($"works/{id}.json", cancellationToken);
        if (response is { IsSuccess: true, Value: { } work }
            && string.Equals(KeyOf(work.Type), "/type/redirect", StringComparison.Ordinal)
            && OlId(work.Location, WorksPrefix, 'W') is { } target)
        {
            return await SendAsync<Work>($"works/{target}.json", cancellationToken);
        }

        return response;
    }

    private async Task<Result<T?>> SendAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        return await ProviderHttp.SendAsync<T>(http, request, Source, ProviderHttp.SnakeCase, logger, cancellationToken);
    }

    private Uri? Cover(long? coverId)
    {
        if (coverId is not > 0)
        {
            return null;
        }

        var cover = new Uri(Settings.CoversBaseUrl, string.Create(CultureInfo.InvariantCulture, $"b/id/{coverId}-L.jpg"));
        return cover.Scheme switch
        {
            "https" => cover,
            "http" => new UriBuilder(cover) { Scheme = Uri.UriSchemeHttps, Port = cover.IsDefaultPort ? -1 : cover.Port }.Uri,
            _ => null,
        };
    }

    private static string? Description(JsonElement description)
    {
        var text = description.ValueKind switch
        {
            JsonValueKind.String => description.GetString(),
            JsonValueKind.Object when description.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String => value.GetString(),
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            text = text.Length > MaxDescriptionInput ? text[..MaxDescriptionInput] : text;
            text = LineBreakTag().Replace(text.ReplaceLineEndings("\n"), "\n");
            // Strip again after decoding so encoded markup ("&lt;b&gt;") cannot turn into tags.
            text = HtmlTag().Replace(WebUtility.HtmlDecode(HtmlTag().Replace(text, string.Empty)), string.Empty);
            text = BlankLines().Replace(text, "\n\n").Trim();
            return text.Length > 0 ? text : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>first_publish_date is free text: "1965", "August 1965", "Aug 01, 1965", "c1965"...</summary>
    private static int? YearOf(string? date)
    {
        var match = date is { Length: <= 100 } ? FourDigits().Match(date) : Match.Empty;
        return match.Success ? PlausibleYear(int.Parse(match.ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture)) : null;
    }

    private static int? PlausibleYear(int? year) => year is >= 1000 and <= 2100 ? year : null;

    /// <summary>"/works/OL45883W" becomes "OL45883W"; anything else is null.</summary>
    private static string? OlId(string? key, string prefix, char suffix) =>
        key is not null && key.StartsWith(prefix, StringComparison.Ordinal) && IsOlid(key[prefix.Length..], suffix)
            ? key[prefix.Length..]
            : null;

    /// <summary>Ids look like OL45883W (work) or OL23919A (author); nothing else may reach a request path.</summary>
    private static bool IsOlid(string id, char suffix) =>
        id.Length is > 3 and <= 20
        && id.StartsWith("OL", StringComparison.Ordinal)
        && id[^1] == suffix
        && !id.AsSpan(2, id.Length - 3).ContainsAnyExceptInRange('0', '9');

    /// <summary>A reference is usually {"key": "/authors/OL1A"}, but some old records hold the bare string.</summary>
    private static string? KeyOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Object when value.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String => key.GetString(),
        _ => null,
    };

    /// <summary>Subjects mix genres with tags such as "nyt:mass-market-monthly=2021-11-07" or "award:nebula_award=novel".</summary>
    private static bool IsMachineTag(string subject)
    {
        var colon = subject.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && !subject.AsSpan(0, colon).ContainsAnyExcept(TagPrefixChars);
    }

    private static IEnumerable<string> Clean(IEnumerable<string?>? values) =>
        (values ?? []).Select(v => v?.Trim()).OfType<string>().Where(v => v.Length > 0).Distinct(StringComparer.Ordinal);

    [GeneratedRegex(@"<br\s*/?>|</p\s*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 100)]
    private static partial Regex LineBreakTag();

    [GeneratedRegex(@"<!--.*?-->|</?[a-zA-Z][^>]*>", RegexOptions.Singleline, matchTimeoutMilliseconds: 100)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\n{3,}", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"(?<!\d)\d{4}(?!\d)", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex FourDigits();
}
