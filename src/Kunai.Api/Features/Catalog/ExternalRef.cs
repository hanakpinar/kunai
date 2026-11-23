using System.Diagnostics.CodeAnalysis;

namespace Kunai.Api.Features.Catalog;

/// <summary>
/// An opaque reference to an item at a provider, e.g. "tmdb:movie:603" or "anilist:manga:30013".
/// The kind is needed because some providers reuse ids across kinds (TMDB movies and TV shows).
/// </summary>
public readonly record struct ExternalRef(ItemSource Source, string Kind, string Id)
{
    private static readonly Dictionary<ItemSource, string> Prefixes = new()
    {
        [ItemSource.Tmdb] = "tmdb",
        [ItemSource.GoogleBooks] = "gbooks",
        [ItemSource.OpenLibrary] = "olib",
        [ItemSource.Igdb] = "igdb",
        [ItemSource.AniList] = "anilist",
    };

    /// <summary>The value stored in items.external_id: kind and id, unique per source.</summary>
    public string StoredId => $"{Kind}:{Id}";

    public override string ToString() => $"{Prefixes[Source]}:{Kind}:{Id}";

    /// <summary>Rebuilds a reference from a stored item; null for manual items.</summary>
    public static ExternalRef? FromStored(ItemSource source, string storedId)
    {
        ArgumentNullException.ThrowIfNull(storedId);
        var separator = storedId.IndexOf(':', StringComparison.Ordinal);
        return source == ItemSource.Manual || separator <= 0
            ? null
            : new ExternalRef(source, storedId[..separator], storedId[(separator + 1)..]);
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out ExternalRef? result)
    {
        result = null;
        var parts = value?.Split(':');
        if (parts is not { Length: 3 } || parts.Any(string.IsNullOrWhiteSpace) || parts[2].Length > 100)
        {
            return false;
        }

        foreach (var (source, prefix) in Prefixes)
        {
            if (string.Equals(prefix, parts[0], StringComparison.Ordinal))
            {
                result = new ExternalRef(source, parts[1], parts[2]);
                return true;
            }
        }

        return false;
    }
}
