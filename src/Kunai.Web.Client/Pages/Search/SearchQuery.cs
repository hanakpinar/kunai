using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

namespace Kunai.Web.Client.Pages.Search;

/// <summary>Reads and builds the /search?q=&amp;type= address, so links and the page agree on the format.</summary>
public static class SearchQuery
{
    /// <summary>The types a search can target, in the order the tabs show them.</summary>
    public static IReadOnlyList<ItemType> Types { get; } = Enum.GetValues<ItemType>();

    /// <summary>The type named in the query string; book when it is missing or unknown.</summary>
    public static ItemType ParseType(string? value) =>
        Types.FirstOrDefault(type => string.Equals(CatalogMetadata.QueryValue(type), value, StringComparison.OrdinalIgnoreCase), ItemType.Book);

    /// <summary>A base-relative search address, e.g. <c>search?q=dune&amp;type=book</c>.</summary>
    public static string Url(string? query, ItemType type) =>
        $"search?q={Uri.EscapeDataString(query ?? string.Empty)}&type={CatalogMetadata.QueryValue(type)}";

    /// <summary>The sign-in page, coming back to <paramref name="returnPath"/> afterwards.</summary>
    public static string LoginUrl(string returnPath) => $"login?returnUrl={Uri.EscapeDataString(returnPath)}";

    /// <summary>
    /// Where a hit's card leads: its page when it is in the catalog, otherwise a page that imports it first.
    /// Null for a hit with neither (the API always sends one of them).
    /// </summary>
    public static string? ItemUrl(SearchItem hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (hit.Id is { } id)
        {
            return $"items/{id}";
        }

        return string.IsNullOrEmpty(hit.Ref) ? null : $"items/open?ref={Uri.EscapeDataString(hit.Ref)}";
    }
}
