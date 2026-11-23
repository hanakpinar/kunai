using System.ComponentModel.DataAnnotations;

using Kunai.Api.Features.Catalog;

namespace Kunai.Api.Features.Library;

/// <summary>The full state of a shelf entry; PUT replaces every field.</summary>
public sealed class ShelfEntryRequest : IValidatableObject
{
    [Required(ErrorMessage = "required")]
    public ShelfStatus? Status { get; init; }

    /// <summary>1-10; not allowed while planned.</summary>
    [Range(1, 10, ErrorMessage = "rating_out_of_range")]
    public int? Rating { get; init; }

    /// <summary>Calendar date in the user's time zone (yyyy-MM-dd).</summary>
    public DateOnly? StartedOn { get; init; }

    /// <summary>Calendar date in the user's time zone; counts toward that year's stats and goals.</summary>
    public DateOnly? FinishedOn { get; init; }

    /// <summary>Pages, episodes, chapters or percent, depending on the item type (see /item-types).</summary>
    [Range(0, 100_000, ErrorMessage = "progress_out_of_range")]
    public int? Progress { get; init; }

    [StringLength(ShelfEntry.ReviewMaxLength, ErrorMessage = "too_long")]
    public string? Review { get; init; }

    public bool IsFavorite { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartedOn is { } started && FinishedOn is { } finished && finished < started)
        {
            yield return new ValidationResult("finished_before_started", [nameof(FinishedOn)]);
        }
    }

    public ShelfEntryData ToData() =>
        new(Status!.Value, Rating, StartedOn, FinishedOn, Progress, Review, IsFavorite);
}
