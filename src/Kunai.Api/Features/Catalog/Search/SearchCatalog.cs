using System.ComponentModel.DataAnnotations;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Catalog.Providers;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Kunai.Api.Features.Catalog.Search;

public static class SearchCatalog
{
    private static readonly HybridCacheEntryOptions CacheFor12Hours = new() { Expiration = TimeSpan.FromHours(12) };

    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet("/search", HandleAsync)
            .WithTags("Catalog")
            .WithName("SearchCatalog")
            .WithSummary("Search a provider for items of one type")
            .WithDescription("Hits that are already in the catalog carry their id; for the rest, call /items/import with the ref. Debounce input (300 ms) and send at least 2 characters.")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Search)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    internal static async Task<Results<Ok<SearchResponse>, ProblemHttpResult>> HandleAsync(
        [AsParameters] SearchRequest request,
        MetadataProviders providers,
        HybridCache cache,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = request.Q!.Trim();
        if (query.Length < 2)
        {
            return Error.ValidationField("q", "query_length", "Search with at least 2 characters.").ToProblem();
        }

        if (SearchCursor.Decode(request.Cursor) is not { } page)
        {
            return Error.Validation(ErrorCodes.InvalidCursor, "The cursor is not valid.").ToProblem();
        }

        var type = Enum.Parse<ItemType>(request.Type!, ignoreCase: true);
        var (result, source, error) = await SearchProvidersAsync(providers.ForSearch(type), type, query, page, cache, cancellationToken);
        if (result is null)
        {
            httpContext.Response.Headers.RetryAfter = "30";
            return (error ?? ProviderErrors.NotConfigured(ItemSource.Manual)).ToProblem();
        }

        var storedIds = result.Hits.Select(h => h.Ref.StoredId).ToList();
        var imported = await db.Set<Item>().AsNoTracking()
            .Where(i => i.Source == source && storedIds.Contains(i.ExternalId))
            .Select(i => new { i.ExternalId, i.Id })
            .ToDictionaryAsync(i => i.ExternalId, i => i.Id, StringComparer.Ordinal, cancellationToken);

        var items = result.Hits
            .Select(hit => new SearchItem(
                hit.Ref.ToString(),
                imported.TryGetValue(hit.Ref.StoredId, out var id) ? id : null,
                hit.Type,
                hit.Title,
                hit.Year,
                hit.Subtitle,
                hit.CoverUrl?.ToString()))
            .ToList();

        return TypedResults.Ok(new SearchResponse(items, result.HasMore ? SearchCursor.Encode(page + 1) : null));
    }

    /// <summary>Tries the type's providers in order (e.g. Google Books, then Open Library) and caches successes.</summary>
    private static async Task<(ProviderSearchPage? Page, ItemSource Source, Error? Error)> SearchProvidersAsync(
        IReadOnlyList<IMetadataProvider> candidates,
        ItemType type,
        string query,
        int page,
        HybridCache cache,
        CancellationToken cancellationToken)
    {
        Error? lastError = null;
        foreach (var provider in candidates)
        {
            var key = $"search:{provider.Source}:{type}:{page}:{query.ToLowerInvariant()}";
            try
            {
                var result = await cache.GetOrCreateAsync(
                    key,
                    async token =>
                    {
                        var search = await provider.SearchAsync(type, query, page, token);
                        return search.IsSuccess ? search.Value : throw new ProviderSearchFailedException(search.Error);
                    },
                    CacheFor12Hours,
                    cancellationToken: cancellationToken);
                return (result, provider.Source, null);
            }
            catch (ProviderSearchFailedException failure)
            {
                lastError = failure.Error;
            }
        }

        return (null, ItemSource.Manual, lastError);
    }

    /// <summary>Carries a provider error out of the cache factory so failures are never cached.</summary>
    private sealed class ProviderSearchFailedException(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }
}

public sealed class SearchRequest
{
    /// <summary>Search text, 2-100 characters.</summary>
    [Required(ErrorMessage = "required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "query_length")]
    public string? Q { get; init; }

    /// <summary>Which kind of item to search for: book, movie, series, game, anime or manga.</summary>
    [Required(ErrorMessage = "required")]
    [AllowedValues("book", "movie", "series", "game", "anime", "manga", ErrorMessage = "type_invalid")]
    public string? Type { get; init; }

    /// <summary>The nextCursor of the previous page.</summary>
    [StringLength(64, ErrorMessage = "too_long")]
    public string? Cursor { get; init; }
}

/// <param name="Items">Search hits in the provider's relevance order.</param>
/// <param name="NextCursor">Pass as "cursor" to get the next page; null on the last page.</param>
public sealed record SearchResponse(IReadOnlyList<SearchItem> Items, string? NextCursor);

/// <param name="Ref">Provider reference; send it to /items/import when Id is null.</param>
/// <param name="Id">The catalog id when the item was already imported.</param>
/// <param name="Type">Item type.</param>
/// <param name="Title">Display title.</param>
/// <param name="Year">Release or publication year.</param>
/// <param name="Subtitle">Secondary line: authors, original title, platforms...</param>
/// <param name="CoverUrl">HTTPS cover image.</param>
public sealed record SearchItem(string Ref, Guid? Id, ItemType Type, string Title, int? Year, string? Subtitle, string? CoverUrl);
