namespace Kunai.Api.Features.Catalog.ItemTypes;

/// <summary>
/// The single source of truth for what each item type allows. Adding a type means adding one entry here
/// (plus its provider); clients read the same data from GET /item-types instead of hard-coding labels.
/// </summary>
public static class ItemTypeRegistry
{
    private static readonly LocalizedText Planned = new("Okuyacağım", "Want to read");

    public static IReadOnlyList<ItemTypeDefinition> All { get; } =
    [
        new(
            ItemType.Book,
            new("Kitap", "Book"),
            [
                new(ShelfStatus.Planned, Planned),
                new(ShelfStatus.InProgress, new("Okuyorum", "Reading")),
                new(ShelfStatus.Completed, new("Okudum", "Read")),
                new(ShelfStatus.Paused, new("Ara verdim", "On hold")),
                new(ShelfStatus.Dropped, new("Yarım bıraktım", "Did not finish")),
            ],
            ProgressUnit.Page,
            [ItemSource.GoogleBooks, ItemSource.OpenLibrary],
            RankPoints: 3),
        new(
            ItemType.Movie,
            new("Film", "Movie"),
            [
                new(ShelfStatus.Planned, new("İzleyeceğim", "Want to watch")),
                new(ShelfStatus.Completed, new("İzledim", "Watched")),
            ],
            null,
            [ItemSource.Tmdb],
            RankPoints: 1),
        new(
            ItemType.Series,
            new("Dizi", "TV series"),
            [
                new(ShelfStatus.Planned, new("İzleyeceğim", "Want to watch")),
                new(ShelfStatus.InProgress, new("İzliyorum", "Watching")),
                new(ShelfStatus.Completed, new("İzledim", "Watched")),
                new(ShelfStatus.Paused, new("Ara verdim", "On hold")),
                new(ShelfStatus.Dropped, new("Bıraktım", "Dropped")),
            ],
            ProgressUnit.Episode,
            [ItemSource.Tmdb],
            RankPoints: 3),
        new(
            ItemType.Game,
            new("Oyun", "Game"),
            [
                new(ShelfStatus.Planned, new("Oynayacağım", "Want to play")),
                new(ShelfStatus.InProgress, new("Oynuyorum", "Playing")),
                new(ShelfStatus.Completed, new("Bitirdim", "Finished")),
                new(ShelfStatus.Paused, new("Ara verdim", "On hold")),
                new(ShelfStatus.Dropped, new("Bıraktım", "Dropped")),
            ],
            ProgressUnit.Percent,
            [ItemSource.Igdb],
            RankPoints: 4),
        new(
            ItemType.Anime,
            new("Anime", "Anime"),
            [
                new(ShelfStatus.Planned, new("İzleyeceğim", "Want to watch")),
                new(ShelfStatus.InProgress, new("İzliyorum", "Watching")),
                new(ShelfStatus.Completed, new("İzledim", "Watched")),
                new(ShelfStatus.Paused, new("Ara verdim", "On hold")),
                new(ShelfStatus.Dropped, new("Yarım bıraktım", "Dropped")),
            ],
            ProgressUnit.Episode,
            [ItemSource.AniList],
            RankPoints: 2),
        new(
            ItemType.Manga,
            new("Manga", "Manga"),
            [
                new(ShelfStatus.Planned, Planned),
                new(ShelfStatus.InProgress, new("Okuyorum", "Reading")),
                new(ShelfStatus.Completed, new("Okudum", "Read")),
                new(ShelfStatus.Paused, new("Ara verdim", "On hold")),
                new(ShelfStatus.Dropped, new("Yarım bıraktım", "Dropped")),
            ],
            ProgressUnit.Chapter,
            [ItemSource.AniList],
            RankPoints: 3),
    ];

    public static IReadOnlyList<SourceAttribution> Sources { get; } =
    [
        new(ItemSource.Tmdb, "TMDB", new Uri("https://www.themoviedb.org"), "This product uses the TMDB API but is not endorsed or certified by TMDB."),
        new(ItemSource.GoogleBooks, "Google Books", new Uri("https://books.google.com"), "Powered by Google"),
        new(ItemSource.OpenLibrary, "Open Library", new Uri("https://openlibrary.org"), "Book data from Open Library"),
        new(ItemSource.Igdb, "IGDB", new Uri("https://www.igdb.com"), "Game data from IGDB.com"),
        new(ItemSource.AniList, "AniList", new Uri("https://anilist.co"), "Anime and manga data from AniList"),
    ];

    public static ItemTypeDefinition For(ItemType type) => All.Single(definition => definition.Type == type);
}
