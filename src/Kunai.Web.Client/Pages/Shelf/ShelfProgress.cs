using System.Globalization;

using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

namespace Kunai.Web.Client.Pages.Shelf;

/// <summary>How far along an entry is, in its type's unit: "120 / 412 sayfa", "8 bölüm" or "%45".</summary>
public sealed record ShelfProgress(string Text, int? Percent)
{
    public static ShelfProgress? Describe(int? progress, int? total, ProgressUnit? unit)
    {
        if (progress is not { } value)
        {
            return null;
        }

        if (unit == ProgressUnit.Percent)
        {
            return new(string.Create(CultureInfo.InvariantCulture, $"%{value}"), Math.Clamp(value, 0, 100));
        }

        var name = CatalogMetadata.ProgressUnitName(unit);
        var suffix = name.Length > 0 ? $" {name}" : string.Empty;
        if (total is { } max && max > 0)
        {
            var percent = Math.Clamp((int)Math.Round(100.0 * value / max), 0, 100);
            return new(string.Create(CultureInfo.InvariantCulture, $"{value} / {max}{suffix}"), percent);
        }

        return new(string.Create(CultureInfo.InvariantCulture, $"{value}{suffix}"), null);
    }
}
