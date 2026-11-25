using Kunai.Web.Client.Api;
using Kunai.Web.Client.Api.Models;

namespace Kunai.Web.Client.Infrastructure;

/// <summary>
/// Item types, their statuses and Turkish labels, loaded once from GET /item-types,
/// so no status name is hard-coded in the UI.
/// </summary>
public sealed class CatalogMetadata(KunaiApiClient api)
{
    private Task<ItemTypesResponse?>? _types;

    /// <summary>The registry, fetched once per session. A failed fetch is forgotten so the next call retries.</summary>
    public async Task<ItemTypesResponse?> GetAsync()
    {
        var load = _types ??= api.Api.V1.ItemTypes.GetAsync();
        try
        {
            return await load;
        }
        catch
        {
            if (ReferenceEquals(_types, load))
            {
                _types = null;
            }

            throw;
        }
    }

    public async Task<ItemTypeDefinition?> ForAsync(ItemType? type) =>
        (await GetAsync())?.Types?.FirstOrDefault(t => t.Type == type);

    public async Task<string> StatusLabelAsync(ItemType? type, ShelfStatus? status) =>
        (await ForAsync(type))?.Statuses?.FirstOrDefault(s => s.Status == status)?.Label?.Tr ?? status?.ToString() ?? string.Empty;

    /// <summary>A label that fits every type, for when the registry cannot be loaded.</summary>
    public static string GenericStatusLabel(ShelfStatus? status) => status switch
    {
        ShelfStatus.Planned => "Planlanan",
        ShelfStatus.InProgress => "Devam eden",
        ShelfStatus.Completed => "Tamamlanan",
        ShelfStatus.Paused => "Ara verilen",
        ShelfStatus.Dropped => "Bırakılan",
        _ => string.Empty,
    };

    public static string TypeName(ItemType? type) => type switch
    {
        ItemType.Book => "Kitap",
        ItemType.Movie => "Film",
        ItemType.Series => "Dizi",
        ItemType.Game => "Oyun",
        ItemType.Anime => "Anime",
        ItemType.Manga => "Manga",
        _ => "İçerik",
    };

    public static string QueryValue(ItemType type) => type switch
    {
        ItemType.Book => "book",
        ItemType.Movie => "movie",
        ItemType.Series => "series",
        ItemType.Game => "game",
        ItemType.Anime => "anime",
        _ => "manga",
    };

    public static string ProgressUnitName(ProgressUnit? unit) => unit switch
    {
        ProgressUnit.Page => "sayfa",
        ProgressUnit.Episode => "bölüm",
        ProgressUnit.Chapter => "bölüm",
        ProgressUnit.Percent => "%",
        _ => string.Empty,
    };
}
