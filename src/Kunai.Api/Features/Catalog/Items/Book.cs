namespace Kunai.Api.Features.Catalog.Items;

public sealed class Book() : Item(ItemType.Book)
{
    public string? Isbn13 { get; set; }

    public string? Publisher { get; set; }
}
