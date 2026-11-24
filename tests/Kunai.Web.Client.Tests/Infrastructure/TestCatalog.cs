namespace Kunai.Web.Client.Tests.Infrastructure;

/// <summary>A trimmed copy of GET /item-types: enough statuses and labels for component tests.</summary>
public static class TestCatalog
{
    public static object ItemTypes { get; } = new
    {
        types = new object[]
        {
            Type("book", "Kitap", "page", 3, ("planned", "Okuyacağım"), ("inProgress", "Okuyorum"), ("completed", "Okudum"), ("paused", "Ara verdim"), ("dropped", "Yarım bıraktım")),
            Type("movie", "Film", null, 1, ("planned", "İzleyeceğim"), ("completed", "İzledim")),
            Type("series", "Dizi", "episode", 3, ("planned", "İzleyeceğim"), ("inProgress", "İzliyorum"), ("completed", "İzledim"), ("paused", "Ara verdim"), ("dropped", "Bıraktım")),
            Type("game", "Oyun", "percent", 4, ("planned", "Oynayacağım"), ("inProgress", "Oynuyorum"), ("completed", "Bitirdim"), ("paused", "Ara verdim"), ("dropped", "Bıraktım")),
            Type("anime", "Anime", "episode", 2, ("planned", "İzleyeceğim"), ("inProgress", "İzliyorum"), ("completed", "İzledim"), ("paused", "Ara verdim"), ("dropped", "Yarım bıraktım")),
            Type("manga", "Manga", "chapter", 3, ("planned", "Okuyacağım"), ("inProgress", "Okuyorum"), ("completed", "Okudum"), ("paused", "Ara verdim"), ("dropped", "Yarım bıraktım")),
        },
        sources = Array.Empty<object>(),
    };

    private static object Type(string type, string name, string? unit, int points, params (string Status, string Tr)[] statuses) => new
    {
        type,
        name = new { tr = name, en = name },
        statuses = statuses.Select(s => new { status = s.Status, label = new { tr = s.Tr, en = s.Status } }).ToArray(),
        progressUnit = unit,
        sources = Array.Empty<string>(),
        rankPoints = points,
    };
}
