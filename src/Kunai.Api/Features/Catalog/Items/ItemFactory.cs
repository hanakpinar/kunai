using Kunai.Api.Features.Catalog.Providers;

namespace Kunai.Api.Features.Catalog.Items;

/// <summary>Creates and refreshes catalog items from provider metadata.</summary>
public static class ItemFactory
{
    public static Item Create(ItemMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var externalId = metadata.Ref.StoredId;
        Item item = metadata.Type switch
        {
            ItemType.Book => new Book { Title = metadata.Title, ExternalId = externalId, Source = metadata.Ref.Source },
            ItemType.Movie => new Movie { Title = metadata.Title, ExternalId = externalId, Source = metadata.Ref.Source },
            ItemType.Series => new Series { Title = metadata.Title, ExternalId = externalId, Source = metadata.Ref.Source },
            ItemType.Game => new Game { Title = metadata.Title, ExternalId = externalId, Source = metadata.Ref.Source },
            ItemType.Anime => new Anime { Title = metadata.Title, ExternalId = externalId, Source = metadata.Ref.Source },
            ItemType.Manga => new Manga { Title = metadata.Title, ExternalId = externalId, Source = metadata.Ref.Source },
            _ => throw new ArgumentOutOfRangeException(nameof(metadata), metadata.Type, "Unknown item type."),
        };

        Apply(item, metadata);
        return item;
    }

    /// <summary>Copies provider metadata onto an item, trimming text to the column limits.</summary>
    public static void Apply(Item item, ItemMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(metadata);

        item.Title = Truncate(metadata.Title, Item.TitleMaxLength)!;
        item.OriginalTitle = Truncate(metadata.OriginalTitle, Item.TitleMaxLength);
        item.Year = metadata.Year is >= 1000 and <= 3000 ? metadata.Year : null;
        item.Description = Truncate(metadata.Description, Item.DescriptionMaxLength);
        item.CoverUrl = metadata.CoverUrl is { Scheme: "https" } cover ? cover.ToString() : null;
        item.Creators = metadata.Creators.Take(20).ToList();
        item.Genres = metadata.Genres.Take(20).ToList();
        item.ProgressTotal = metadata.ProgressTotal is > 0 ? metadata.ProgressTotal : null;

        switch (item)
        {
            case Book book:
                book.Isbn13 = metadata.Isbn13;
                book.Publisher = Truncate(metadata.Publisher, 200);
                break;
            case Movie movie:
                movie.RuntimeMinutes = metadata.RuntimeMinutes;
                break;
            case Series series:
                series.SeasonCount = metadata.SeasonCount;
                break;
            case Game game:
                game.Platforms = metadata.Platforms.Take(30).ToList();
                break;
            case Anime anime:
                anime.Format = Truncate(metadata.Format, 32);
                break;
            case Manga manga:
                manga.Format = Truncate(metadata.Format, 32);
                manga.VolumeCount = metadata.VolumeCount;
                break;
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
