using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Catalog.ItemTypes;
using Kunai.Api.Features.Identity;
using Kunai.Api.Features.Library;
using Kunai.Api.Features.Profiles.Avatars;
using Kunai.Api.Features.Profiles.Ranks;
using Kunai.Api.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Features.Profiles;

/// <summary>Computes the public profile: counts per type and status, this year's completions, belt and favorites.</summary>
public static class ProfileSummary
{
    public const int FavoritesShown = 8;

    public static async Task<PublicProfileResponse> ComputeAsync(AppDbContext db, AppUser user, TimeProvider time, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(user);

        var groups = await db.Set<ShelfEntry>().AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Item.CatalogStatus == CatalogStatus.Approved)
            .GroupBy(e => new { e.Item.Type, e.Status })
            .Select(g => new { g.Key.Type, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var year = UserClock.Today(time, user.TimeZone).Year;
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var thisYear = await db.Set<ShelfEntry>().AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Item.CatalogStatus == CatalogStatus.Approved && e.Status == ShelfStatus.Completed && e.FinishedOn >= from && e.FinishedOn <= to)
            .GroupBy(e => e.Item.Type)
            .Select(g => new TypeCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var favorites = await db.Set<ShelfEntry>().AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Item.CatalogStatus == CatalogStatus.Approved && e.IsFavorite)
            .OrderByDescending(e => e.UpdatedAt)
            .Take(FavoritesShown)
            .Select(e => e.Item)
            .ToListAsync(cancellationToken);

        var counts = Enum.GetValues<ItemType>()
            .Select(type => new TypeCounts(
                type,
                Count(type, ShelfStatus.Planned),
                Count(type, ShelfStatus.InProgress),
                Count(type, ShelfStatus.Completed),
                Count(type, ShelfStatus.Paused),
                Count(type, ShelfStatus.Dropped)))
            .ToList();

        var points = counts.Sum(c => c.Completed * ItemTypeRegistry.For(c.Type).RankPoints);
        var rank = RankResponse.For(points);

        return new PublicProfileResponse(
            user.UserName!,
            user.DisplayName,
            user.Bio,
            AvatarRegistry.UrlFor(user.AvatarId, rank.Code),
            user.CreatedAt,
            rank,
            counts,
            new ThisYearResponse(year, thisYear),
            favorites.Select(ItemSummary.From).ToList());

        int Count(ItemType type, ShelfStatus status) =>
            groups.Where(g => g.Type == type && g.Status == status).Sum(g => g.Count);
    }
}

/// <param name="Username">Public handle.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Bio">Short bio.</param>
/// <param name="AvatarUrl">Relative URL of the ninja avatar, painted with the current belt.</param>
/// <param name="JoinedAt">When the account was created.</param>
/// <param name="Rank">Current belt and progress to the next one.</param>
/// <param name="Counts">Entries per type and status.</param>
/// <param name="ThisYear">Items completed this calendar year (in the user's time zone).</param>
/// <param name="Favorites">Most recently favorited items.</param>
public sealed record PublicProfileResponse(
    string Username,
    string DisplayName,
    string? Bio,
    string AvatarUrl,
    DateTimeOffset JoinedAt,
    RankResponse Rank,
    IReadOnlyList<TypeCounts> Counts,
    ThisYearResponse ThisYear,
    IReadOnlyList<ItemSummary> Favorites);

public sealed record TypeCounts(ItemType Type, int Planned, int InProgress, int Completed, int Paused, int Dropped);

public sealed record TypeCount(ItemType Type, int Count);

public sealed record ThisYearResponse(int Year, IReadOnlyList<TypeCount> Completed);
