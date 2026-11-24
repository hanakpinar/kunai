using System.Globalization;

using Kunai.Web.Client.Api.Models;

namespace Kunai.Web.Client.Pages.Search.Items;

/// <summary>Turns an item's type-specific fields into Turkish label/value pairs, skipping the ones that are empty.</summary>
public static class ItemFacts
{
    public static IReadOnlyList<ItemFact> For(ItemResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var details = item.Details;
        var facts = new List<ItemFact>();

        if (item.ProgressTotal is > 0 and var total && TotalLabel(item.Type) is { } totalLabel)
        {
            facts.Add(new(totalLabel, Number(total)));
        }

        if (details is null)
        {
            return facts;
        }

        if (details.SeasonCount is > 0 and var seasons)
        {
            facts.Add(new("Sezon", Number(seasons)));
        }

        if (details.VolumeCount is > 0 and var volumes)
        {
            facts.Add(new("Cilt", Number(volumes)));
        }

        if (details.RuntimeMinutes is > 0 and var minutes)
        {
            facts.Add(new(item.Type is ItemType.Series or ItemType.Anime ? "Bölüm süresi" : "Süre", Runtime(minutes)));
        }

        if (details.Platforms is { Count: > 0 } platforms)
        {
            facts.Add(new("Platformlar", string.Join(", ", platforms)));
        }

        Add(facts, "Format", details.Format);
        Add(facts, "Yayınevi", details.Publisher);
        Add(facts, "ISBN-13", details.Isbn13);
        return facts;
    }

    /// <summary>136 → "2 sa 16 dk", 45 → "45 dk".</summary>
    public static string Runtime(int minutes) => (minutes / 60, minutes % 60) switch
    {
        (0, var m) => $"{Number(m)} dk",
        (var h, 0) => $"{Number(h)} sa",
        (var h, var m) => $"{Number(h)} sa {Number(m)} dk",
    };

    private static string? TotalLabel(ItemType? type) => type switch
    {
        ItemType.Book => "Sayfa sayısı",
        ItemType.Series or ItemType.Anime or ItemType.Manga => "Bölüm sayısı",
        _ => null,
    };

    private static void Add(List<ItemFact> facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Add(new(label, value));
        }
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
