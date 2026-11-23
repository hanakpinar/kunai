using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.ItemTypes;

namespace Kunai.Api.UnitTests.Catalog;

public sealed class ItemTypeRegistryTests
{
    [Fact]
    public void Every_item_type_is_registered_once() =>
        ItemTypeRegistry.All.Select(d => d.Type).Should().BeEquivalentTo(Enum.GetValues<ItemType>());

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_type_allows_planned_and_completed_and_has_labels(ItemType type)
    {
        var definition = ItemTypeRegistry.For(type);

        definition.Allows(ShelfStatus.Planned).Should().BeTrue();
        definition.Allows(ShelfStatus.Completed).Should().BeTrue();
        definition.Statuses.Should().OnlyContain(s => s.Label.Tr.Length > 0 && s.Label.En.Length > 0);
        definition.RankPoints.Should().BePositive();
        definition.Sources.Should().NotBeEmpty();
    }

    [Fact]
    public void Movies_have_no_in_progress_status() =>
        ItemTypeRegistry.For(ItemType.Movie).Allows(ShelfStatus.InProgress).Should().BeFalse();

    [Fact]
    public void Every_source_used_by_a_type_has_attribution() =>
        ItemTypeRegistry.All.SelectMany(d => d.Sources).Distinct()
            .Should().BeSubsetOf(ItemTypeRegistry.Sources.Select(s => s.Source));

    public static TheoryData<ItemType> Types() => new(Enum.GetValues<ItemType>());
}
