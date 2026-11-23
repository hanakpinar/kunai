namespace Kunai.Api.Features.Catalog.Items;

public sealed class Anime() : Item(ItemType.Anime)
{
    /// <summary>TV, MOVIE, OVA, ONA, SPECIAL...</summary>
    public string? Format { get; set; }
}
