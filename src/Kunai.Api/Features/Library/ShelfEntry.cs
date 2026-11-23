using Kunai.Api.Common.Persistence;
using Kunai.Api.Common.Results;
using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Catalog.ItemTypes;

namespace Kunai.Api.Features.Library;

/// <summary>
/// One item on one user's shelf. The aggregate enforces the rules that need the item (allowed statuses,
/// progress limits); rules that only need the request (rating range, date order) are validated earlier.
/// Dates are calendar dates in the user's own time zone and are stored exactly as the client sends them.
/// </summary>
public sealed class ShelfEntry : IAuditable
{
    public const int ReviewMaxLength = 2000;

    private ShelfEntry()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid ItemId { get; private set; }

    public Item Item { get; private set; } = null!;

    public ShelfStatus Status { get; private set; }

    public int? Rating { get; private set; }

    public DateOnly? StartedOn { get; private set; }

    public DateOnly? FinishedOn { get; private set; }

    public int? Progress { get; private set; }

    public string? Review { get; private set; }

    public bool IsFavorite { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>PostgreSQL xmin; changes on every update. Exposed to clients as the ETag.</summary>
    public uint Version { get; private set; }

    public static Result<ShelfEntry> Create(Guid userId, Item item, ShelfEntryData data)
    {
        ArgumentNullException.ThrowIfNull(item);
        var entry = new ShelfEntry { UserId = userId, ItemId = item.Id, Item = item };
        var applied = entry.Apply(data);
        return applied.IsSuccess ? entry : applied.Error;
    }

    /// <summary>Replaces every field (PUT semantics). Nothing changes when a rule is broken.</summary>
    public Result Update(ShelfEntryData data) => Apply(data);

    private Result Apply(ShelfEntryData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (ShelfRules.Check(Item, data) is { } error)
        {
            return error;
        }

        Status = data.Status;
        Rating = data.Rating;
        StartedOn = data.StartedOn;
        FinishedOn = data.FinishedOn;
        Progress = data.Progress;
        Review = string.IsNullOrWhiteSpace(data.Review) ? null : data.Review.Trim();
        IsFavorite = data.IsFavorite;
        return Result.Success;
    }
}

public sealed record ShelfEntryData(
    ShelfStatus Status,
    int? Rating,
    DateOnly? StartedOn,
    DateOnly? FinishedOn,
    int? Progress,
    string? Review,
    bool IsFavorite);

/// <summary>Business rules that depend on the item. Each broken rule is a 422 with a stable code.</summary>
public static class ShelfRules
{
    public const string StatusNotAllowed = "status_not_allowed";
    public const string PlannedHasNoActivity = "planned_has_no_activity";
    public const string ProgressNotTracked = "progress_not_tracked";
    public const string ProgressExceedsTotal = "progress_exceeds_total";

    public static Error? Check(Item item, ShelfEntryData data)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(data);
        var definition = ItemTypeRegistry.For(item.Type);

        if (!definition.Allows(data.Status))
        {
            return Rule("status", StatusNotAllowed, $"A {item.Type} cannot have the status {data.Status}.");
        }

        if (data.Status == ShelfStatus.Planned && (data.Rating is not null || data.StartedOn is not null || data.FinishedOn is not null || data.Progress is not null))
        {
            return Rule("status", PlannedHasNoActivity, "Planned items have no rating, dates or progress yet.");
        }

        if (data.Progress is { } progress)
        {
            if (definition.ProgressUnit is null)
            {
                return Rule("progress", ProgressNotTracked, $"Progress is not tracked for a {item.Type}.");
            }

            var maximum = definition.ProgressUnit == ProgressUnit.Percent ? 100 : item.ProgressTotal;
            if (progress > maximum)
            {
                return Rule("progress", ProgressExceedsTotal, $"Progress cannot be more than {maximum}.");
            }
        }

        return null;
    }

    private static Error Rule(string field, string code, string message) =>
        new(code, message, ErrorType.BusinessRule) { Fields = new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [code] } };
}
