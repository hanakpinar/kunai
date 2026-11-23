namespace Kunai.Api.Features.Catalog.Items;

public sealed class Book : Item
{
    public string? Isbn13 { get; set; }

    public string? Publisher { get; set; }
}
