namespace Kunai.Api.Features.Catalog.Items;

public sealed class Manga() : Item(ItemType.Manga)
{
    /// <summary>MANGA, NOVEL (light novel), ONE_SHOT...</summary>
    public string? Format { get; set; }

    public int? VolumeCount { get; set; }
}
