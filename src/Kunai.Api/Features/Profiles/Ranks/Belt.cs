using Kunai.Api.Features.Catalog.ItemTypes;

namespace Kunai.Api.Features.Profiles.Ranks;

/// <param name="Code">Stable code: white, yellow, orange, green, blue, brown, black.</param>
/// <param name="Label">Display name.</param>
/// <param name="MinPoints">Points needed to reach this belt.</param>
/// <param name="Color">Hex color used to paint the avatar's belt.</param>
public sealed record Belt(string Code, LocalizedText Label, int MinPoints, string Color);

/// <summary>Belt ranks (judo order). Points come from completed items, weighted by type (see /item-types rankPoints).</summary>
public static class Belts
{
    public static IReadOnlyList<Belt> All { get; } =
    [
        new("white", new("Beyaz kuşak", "White belt"), 0, "#f4f4f0"),
        new("yellow", new("Sarı kuşak", "Yellow belt"), 25, "#f5c518"),
        new("orange", new("Turuncu kuşak", "Orange belt"), 75, "#f08a24"),
        new("green", new("Yeşil kuşak", "Green belt"), 150, "#2e9e4f"),
        new("blue", new("Mavi kuşak", "Blue belt"), 300, "#2f6fd6"),
        new("brown", new("Kahverengi kuşak", "Brown belt"), 600, "#7a4a24"),
        new("black", new("Siyah kuşak", "Black belt"), 1000, "#141414"),
    ];

    public static Belt ForPoints(int points) => All.Last(belt => points >= belt.MinPoints);

    public static Belt? Next(Belt current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var index = All.ToList().IndexOf(current);
        return index + 1 < All.Count ? All[index + 1] : null;
    }

    public static Belt? Find(string? code) => All.FirstOrDefault(b => string.Equals(b.Code, code, StringComparison.Ordinal));
}

/// <param name="Code">Current belt code.</param>
/// <param name="Label">Current belt name.</param>
/// <param name="Points">Points earned so far.</param>
/// <param name="NextCode">The next belt, or null at black belt.</param>
/// <param name="NextAt">Points needed for the next belt, or null at black belt.</param>
public sealed record RankResponse(string Code, LocalizedText Label, int Points, string? NextCode, int? NextAt)
{
    public static RankResponse For(int points)
    {
        var belt = Belts.ForPoints(points);
        var next = Belts.Next(belt);
        return new RankResponse(belt.Code, belt.Label, points, next?.Code, next?.MinPoints);
    }
}
