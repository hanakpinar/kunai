using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

namespace Kunai.Web.Client.Pages.Search;

/// <summary>Reads and builds the /search?q=&amp;type= address, so links and the page agree on the format.</summary>
public static class SearchQuery
{
    /// <summary>The shortest search text the API accepts.</summary>
    public const int MinLength = 2;

    /// <summary>The longest search text the API accepts; longer text answers 400 query_length.</summary>
    public const int MaxLength = 100;

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
    /// Importing needs an account, so a signed-out visitor goes to sign in first and comes back to it.
    /// Null for a hit with neither (the API always sends one of them).
    /// </summary>
    public static string? ItemUrl(SearchItem hit, bool signedIn)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (hit.Id is { } id)
        {
            return $"items/{id}";
        }

        if (string.IsNullOrEmpty(hit.Ref))
        {
            return null;
        }

        var open = $"items/open?ref={Uri.EscapeDataString(hit.Ref)}";
        return signedIn ? open : LoginUrl(open);
    }

    /// <summary>
    /// What identifies a hit across pages: the provider ref, or the catalog id for a book added by hand.
    /// Null when the hit has neither, so it is never mistaken for another one.
    /// </summary>
    public static string? HitKey(SearchItem hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        return hit.Ref ?? hit.Id?.ToString();
    }
}
