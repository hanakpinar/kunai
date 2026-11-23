namespace Kunai.Api.Features.Catalog.Items;

public sealed class Movie() : Item(ItemType.Movie)
{
    public int? RuntimeMinutes { get; set; }
}
