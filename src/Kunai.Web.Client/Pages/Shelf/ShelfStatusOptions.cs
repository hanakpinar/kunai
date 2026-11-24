using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

using Microsoft.Kiota.Abstractions;

namespace Kunai.Web.Client.Pages.Shelf;

/// <summary>
/// The status chips for a type tab: the type's own statuses and labels from GET /item-types, or neutral
/// labels on "Tümü", where one type's verbs ("Okuyorum") would not fit a mixed list.
/// </summary>
public static class ShelfStatusOptions
{
    public static IReadOnlyList<StatusOption> Generic { get; } =
    [
        new(ShelfStatus.Planned, "Planlanan"),
        new(ShelfStatus.InProgress, "Devam eden"),
        new(ShelfStatus.Completed, "Tamamlanan"),
        new(ShelfStatus.Paused, "Ara verilen"),
        new(ShelfStatus.Dropped, "Bırakılan"),
    ];

    public static async Task<IReadOnlyList<StatusOption>> ForAsync(CatalogMetadata catalog, ItemType? type)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (type is null)
        {
            return Generic;
        }

        try
        {
            var statuses = (await catalog.ForAsync(type))?.Statuses ?? [];
            var options = statuses
                .Where(s => s.Status is not null)
                .Select(s => new StatusOption(s.Status!.Value, s.Label?.Tr ?? s.Status.Value.ToString()))
                .ToArray();
            if (options.Length > 0)
            {
                return options;
            }
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or OperationCanceledException)
        {
            // Without the catalog (unreachable, failing or timed out) the neutral labels still let the user filter.
        }

        return Generic;
    }
}

/// <summary>A status chip: the status and its Turkish label.</summary>
public sealed record StatusOption(ShelfStatus Status, string Label);
