using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Features.Library;

namespace Kunai.Api.UnitTests.Library;

public sealed class ShelfRulesTests
{
    private static readonly Book Book = new() { Title = "Dune", ExternalId = "volume:1", ProgressTotal = 412 };
    private static readonly Movie Movie = new() { Title = "Arrival", ExternalId = "movie:1" };

    [Fact]
    public void A_completed_book_with_rating_dates_and_full_progress_is_valid() =>
        ShelfRules.Check(Book, new ShelfEntryData(ShelfStatus.Completed, 9, new(2026, 11, 1), new(2026, 11, 20), 412, "Great", true))
            .Should().BeNull();

    [Theory]
    [InlineData(ShelfStatus.InProgress)]
    [InlineData(ShelfStatus.Paused)]
    public void Movies_only_allow_planned_and_completed(ShelfStatus status) =>
        ShelfRules.Check(Movie, new ShelfEntryData(status, null, null, null, null, null, false))!.Code
            .Should().Be(ShelfRules.StatusNotAllowed);

    [Fact]
    public void Planned_entries_have_no_activity() =>
        ShelfRules.Check(Book, new ShelfEntryData(ShelfStatus.Planned, null, new(2026, 1, 1), null, null, null, false))!.Code
            .Should().Be(ShelfRules.PlannedHasNoActivity);

    [Fact]
    public void Progress_cannot_exceed_the_item_total() =>
        ShelfRules.Check(Book, new ShelfEntryData(ShelfStatus.InProgress, null, null, null, 413, null, false))!.Code
            .Should().Be(ShelfRules.ProgressExceedsTotal);

    [Fact]
    public void Progress_is_unbounded_when_the_total_is_unknown() =>
        ShelfRules.Check(new Book { Title = "x", ExternalId = "volume:2" }, new ShelfEntryData(ShelfStatus.InProgress, null, null, null, 5000, null, false))
            .Should().BeNull();

    [Fact]
    public void Failed_update_leaves_the_entry_unchanged()
    {
        var entry = ShelfEntry.Create(Guid.NewGuid(), Movie, new ShelfEntryData(ShelfStatus.Planned, null, null, null, null, null, false)).Value;

        var result = entry.Update(new ShelfEntryData(ShelfStatus.InProgress, null, null, null, null, null, true));

        result.IsSuccess.Should().BeFalse();
        entry.Status.Should().Be(ShelfStatus.Planned);
        entry.IsFavorite.Should().BeFalse();
    }
}
