namespace Kunai.Api.Features.Catalog.Items;

public sealed class Game : Item
{
    public IList<string> Platforms { get; set; } = [];
}
