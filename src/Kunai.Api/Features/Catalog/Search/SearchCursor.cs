using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.WebUtilities;

namespace Kunai.Api.Features.Catalog.Search;

/// <summary>Search pages are provider pages; the cursor wraps the page number so clients treat it as opaque.</summary>
public static class SearchCursor
{
    public static string Encode(int page) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("p" + page.ToString(CultureInfo.InvariantCulture)));

    public static int? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return 1;
        }

        try
        {
            var text = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor));
            return text.StartsWith('p')
                && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var page)
                && page is >= 1 and <= 50
                ? page
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
