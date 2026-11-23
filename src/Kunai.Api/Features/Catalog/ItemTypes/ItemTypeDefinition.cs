namespace Kunai.Api.Features.Catalog.ItemTypes;

/// <param name="Type">The item type.</param>
/// <param name="Name">Display name, singular.</param>
/// <param name="Statuses">The statuses this type allows, in display order, with their labels.</param>
/// <param name="ProgressUnit">What progress counts (pages, episodes...); null when progress is not tracked.</param>
/// <param name="Sources">Providers searched for this type.</param>
/// <param name="RankPoints">Belt points earned for each completed item of this type.</param>
public sealed record ItemTypeDefinition(
    ItemType Type,
    LocalizedText Name,
    IReadOnlyList<StatusDefinition> Statuses,
    ProgressUnit? ProgressUnit,
    IReadOnlyList<ItemSource> Sources,
    int RankPoints)
{
    public bool Allows(ShelfStatus status) => Statuses.Any(s => s.Status == status);
}

public sealed record StatusDefinition(ShelfStatus Status, LocalizedText Label);

/// <param name="Source">The provider.</param>
/// <param name="Name">Provider name to display.</param>
/// <param name="Url">Link to the provider.</param>
/// <param name="Notice">Attribution text that must be shown where the data appears.</param>
public sealed record SourceAttribution(ItemSource Source, string Name, Uri Url, string Notice);
