namespace Kunai.Web.Client.Pages.Search.Items;

/// <summary>One labelled line of an item's type-specific details, e.g. "Sayfa sayısı: 412".</summary>
public sealed record ItemFact(string Label, string Value);
