using System.Globalization;

namespace Kunai.Api.Features.Profiles.Avatars;

/// <summary>Twelve built-in ninja avatars: ninja-01 ... ninja-12. Users pick one; uploads come later.</summary>
public static class AvatarRegistry
{
    public const int Count = 12;

    public static IReadOnlyList<string> Ids { get; } =
        Enumerable.Range(1, Count).Select(i => "ninja-" + i.ToString("00", CultureInfo.InvariantCulture)).ToList();

    public static bool Exists(string? id) => id is not null && Ids.Contains(id, StringComparer.Ordinal);

    /// <summary>A stable pick per user, so new accounts do not all look the same.</summary>
    public static string DefaultFor(Guid userId) => Ids[(int)((uint)userId.GetHashCode() % Count)];

    /// <summary>Relative URL of the avatar painted with the user's belt color.</summary>
    public static string UrlFor(string avatarId, string beltCode) => $"/api/v1/avatars/{avatarId}.svg?belt={beltCode}";
}
