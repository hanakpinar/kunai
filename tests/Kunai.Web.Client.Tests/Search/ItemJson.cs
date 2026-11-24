namespace Kunai.Web.Client.Tests.Search;

/// <summary>Item responses shaped like the API sends them (camelCase, enum strings, every field present).</summary>
internal static class ItemJson
{
    public static object Movie(Guid id) => new
    {
        id,
        type = "movie",
        title = "Dune",
        originalTitle = (string?)null,
        year = 2021,
        description = "Paul Atreides çöl gezegeni Arrakis'e gider.",
        coverUrl = (string?)null,
        creators = new[] { "Denis Villeneuve" },
        genres = new[] { "Bilim Kurgu", "Macera" },
        progressTotal = (int?)null,
        source = "tmdb",
        @ref = "tmdb:movie:438631",
        catalogStatus = "approved",
        details = Details(runtimeMinutes: 155),
    };

    public static object Book(Guid id, string catalogStatus = "approved", string source = "openLibrary") => new
    {
        id,
        type = "book",
        title = "Kürk Mantolu Madonna",
        originalTitle = "Kürk Mantolu Madonna",
        year = 1943,
        description = "Raif Efendi'nin defteri.\nBerlin'de bir sergi.",
        coverUrl = "https://covers.example/madonna.jpg",
        creators = new[] { "Sabahattin Ali" },
        genres = new[] { "Roman" },
        progressTotal = 160,
        source,
        @ref = string.Equals(source, "manual", StringComparison.Ordinal) ? null : "openLibrary:OL1W",
        catalogStatus,
        details = Details(isbn13: "9789753638029", publisher: "Yapı Kredi Yayınları"),
    };

    private static object Details(string? isbn13 = null, string? publisher = null, int? runtimeMinutes = null) => new
    {
        isbn13,
        publisher,
        runtimeMinutes,
        seasonCount = (int?)null,
        platforms = Array.Empty<string>(),
        format = (string?)null,
        volumeCount = (int?)null,
    };
}
