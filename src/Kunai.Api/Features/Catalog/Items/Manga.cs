namespace Kunai.Api.Features.Catalog.Items;

public sealed class Manga : Item
{
    /// <summary>MANGA, NOVEL (light novel), ONE_SHOT...</summary>
    public string? Format { get; set; }

    public int? VolumeCount { get; set; }
}
