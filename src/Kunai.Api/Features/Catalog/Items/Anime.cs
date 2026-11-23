namespace Kunai.Api.Features.Catalog.Items;

public sealed class Anime : Item
{
    /// <summary>TV, MOVIE, OVA, ONA, SPECIAL...</summary>
    public string? Format { get; set; }
}
