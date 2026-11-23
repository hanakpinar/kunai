namespace Kunai.Api.Features.Catalog.Items;

public sealed class Series() : Item(ItemType.Series)
{
    public int? SeasonCount { get; set; }
}
