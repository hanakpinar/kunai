using Kunai.Api.Common.Persistence;

namespace Kunai.Api.Features.Catalog.Items;

/// <summary>
/// A book, movie, series, game, anime or manga in the shared catalog. Items are imported from providers
/// (or added by hand for books) once, and every user's shelf points at the same row.
/// </summary>
public abstract class Item : IAuditable
{
    public const int TitleMaxLength = 500;
    public const int DescriptionMaxLength = 5000;

    /// <param name="type">Set by each subclass, so the type is right before EF Core ever sees the item.</param>
    protected Item(ItemType type) => Type = type;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public ItemType Type { get; private set; }

    public required string Title { get; set; }

    public string? OriginalTitle { get; set; }

    public int? Year { get; set; }

    public string? Description { get; set; }

    public string? CoverUrl { get; set; }

    /// <summary>Authors, directors, developers or studios, depending on the type.</summary>
    public IList<string> Creators { get; set; } = [];

    public IList<string> Genres { get; set; } = [];

    /// <summary>Pages, episodes or chapters; null when unknown or not applicable.</summary>
    public int? ProgressTotal { get; set; }

    public ItemSource Source { get; init; }

    /// <summary>The provider's id, or this item's id for manual entries.</summary>
    public required string ExternalId { get; init; }

    public CatalogStatus CatalogStatus { get; set; } = CatalogStatus.Approved;

    /// <summary>Who added a manual item; null for imported items.</summary>
    public Guid? CreatedByUserId { get; init; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }
}

/// <summary>Manually added items wait for a moderator before other users can find them.</summary>
public enum CatalogStatus
{
    Approved,
    PendingReview,
    Rejected,
}
