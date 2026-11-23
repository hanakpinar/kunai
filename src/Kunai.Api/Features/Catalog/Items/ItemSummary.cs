namespace Kunai.Api.Features.Catalog.Items;

/// <summary>The few item fields a shelf or profile list needs.</summary>
public sealed record ItemSummary(
    Guid Id,
    ItemType Type,
    string Title,
    int? Year,
    string? CoverUrl,
    IReadOnlyList<string> Creators,
    int? ProgressTotal)
{
    public static ItemSummary From(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemSummary(item.Id, item.Type, item.Title, item.Year, item.CoverUrl, item.Creators.ToList(), item.ProgressTotal);
    }
}
