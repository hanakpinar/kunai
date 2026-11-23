using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Kunai.Api.Common.Caching;
using Kunai.Api.Common.Http;
using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Library;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Kunai.Api.Features.Profiles.Stats;

public static class GetUserStats
{
    private static readonly HybridCacheEntryOptions TenMinutes = new() { Expiration = TimeSpan.FromMinutes(10) };

    public static void Map(RouteGroupBuilder users) =>
        users.MapGet("/{username}/stats", HandleAsync)
            .WithName("GetUserStats")
            .WithSummary("Yearly reading/watching/playing stats: completions per type and month, average ratings, pages and episodes")
            .AllowAnonymous()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

    internal static async Task<Results<Ok<StatsResponse>, ProblemHttpResult>> HandleAsync(
        string username,
        [Range(1000, 3000, ErrorMessage = "year_out_of_range")] int? year,
        ClaimsPrincipal viewer,
        AppDbContext db,
        HybridCache cache,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var user = await ProfileLookup.FindVisibleAsync(db, username, viewer, cancellationToken);
        if (user is null)
        {
            return ProfileLookup.NotFound.ToProblem();
        }

        var selectedYear = year ?? UserClock.Today(time, user.TimeZone).Year;
        var stats = await cache.GetOrCreateAsync(
            $"stats:{user.Id}:{selectedYear}",
            async token => await ComputeAsync(db, user.Id, selectedYear, token),
            TenMinutes,
            [CacheTags.User(user.Id)],
            cancellationToken);
        return TypedResults.Ok(stats);
    }

    private static async Task<StatsResponse> ComputeAsync(AppDbContext db, Guid userId, int year, CancellationToken cancellationToken)
    {
        var completed = db.Set<ShelfEntry>().AsNoTracking()
            .Where(e => e.UserId == userId && e.Item.CatalogStatus == CatalogStatus.Approved && e.Status == ShelfStatus.Completed && e.FinishedOn != null);

        var years = await completed.Select(e => e.FinishedOn!.Value.Year).Distinct().OrderByDescending(y => y).ToListAsync(cancellationToken);

        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var rows = await completed
            .Where(e => e.FinishedOn >= from && e.FinishedOn <= to)
            .Select(e => new { e.Item.Type, Month = e.FinishedOn!.Value.Month, e.Rating, e.Item.ProgressTotal })
            .ToListAsync(cancellationToken);

        var types = rows.GroupBy(r => r.Type)
            .OrderBy(g => g.Key)
            .Select(g => new TypeStats(
                g.Key,
                g.Count(),
                g.Any(r => r.Rating is not null) ? Math.Round(g.Where(r => r.Rating is not null).Average(r => r.Rating!.Value), 1) : null))
            .ToList();

        var byMonth = Enumerable.Range(1, 12).Select(month => rows.Count(r => r.Month == month)).ToList();

        int Sum(params ItemType[] kinds) => rows.Where(r => kinds.Contains(r.Type)).Sum(r => r.ProgressTotal ?? 0);

        return new StatsResponse(
            year,
            years,
            types,
            byMonth,
            PagesRead: Sum(ItemType.Book),
            EpisodesWatched: Sum(ItemType.Series, ItemType.Anime),
            ChaptersRead: Sum(ItemType.Manga));
    }
}

/// <param name="Year">The year these numbers cover.</param>
/// <param name="AvailableYears">Years with at least one completion, newest first.</param>
/// <param name="Types">Completions and average rating per type.</param>
/// <param name="CompletedByMonth">Twelve numbers, January first.</param>
/// <param name="PagesRead">Total pages of the books completed (when the page count is known).</param>
/// <param name="EpisodesWatched">Total episodes of the series and anime completed.</param>
/// <param name="ChaptersRead">Total chapters of the manga completed.</param>
public sealed record StatsResponse(
    int Year,
    IReadOnlyList<int> AvailableYears,
    IReadOnlyList<TypeStats> Types,
    IReadOnlyList<int> CompletedByMonth,
    int PagesRead,
    int EpisodesWatched,
    int ChaptersRead);

public sealed record TypeStats(ItemType Type, int Completed, double? AverageRating);
