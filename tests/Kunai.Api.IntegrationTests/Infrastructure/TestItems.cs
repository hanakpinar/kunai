using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Catalog.Items;
using Kunai.Api.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Api.IntegrationTests.Infrastructure;

/// <summary>Inserts catalog items straight into the database, for tests that are not about importing.</summary>
public static class TestItems
{
    public static async Task<Guid> CreateItemAsync(this KunaiApiFactory factory, ItemType type, int? progressTotal = null, string? title = null)
    {
        var externalId = $"test:{Guid.NewGuid():N}";
        title ??= $"{type} {externalId[^6..]}";
        Item item = type switch
        {
            ItemType.Book => new Book { Title = title, ExternalId = externalId, Source = ItemSource.GoogleBooks },
            ItemType.Movie => new Movie { Title = title, ExternalId = externalId, Source = ItemSource.Tmdb },
            ItemType.Series => new Series { Title = title, ExternalId = externalId, Source = ItemSource.Tmdb },
            ItemType.Game => new Game { Title = title, ExternalId = externalId, Source = ItemSource.Igdb },
            ItemType.Anime => new Anime { Title = title, ExternalId = externalId, Source = ItemSource.AniList },
            _ => new Manga { Title = title, ExternalId = externalId, Source = ItemSource.AniList },
        };
        item.ProgressTotal = progressTotal;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(item);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item.Id;
    }
}
