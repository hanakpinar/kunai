namespace Kunai.Api.Features.Catalog.Items;

public sealed class Game() : Item(ItemType.Game)
{
    public IList<string> Platforms { get; set; } = [];
}
