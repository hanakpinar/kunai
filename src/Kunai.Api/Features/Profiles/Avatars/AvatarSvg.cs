using System.Globalization;

namespace Kunai.Api.Features.Profiles.Avatars;

/// <summary>Draws a ninja avatar from a fixed palette; no user input ever reaches the SVG.</summary>
public static class AvatarSvg
{
    private static readonly string[] Backgrounds = ["#fde8d7", "#e3f0ff", "#e6f6ea", "#f3e8ff", "#fff4cc", "#ffe3e8"];
    private static readonly string[] Suits = ["#1f2430", "#2b2140", "#13332a", "#3a1f1f"];
    private static readonly string[] Bands = ["#d7263d", "#1b998b", "#f46036", "#2e86ab", "#c5d86d", "#8e44ad"];

    public static string Render(string avatarId, string beltColor)
    {
        var index = AvatarRegistry.Ids.ToList().IndexOf(avatarId);
        var background = Backgrounds[index % Backgrounds.Length];
        var suit = Suits[index % Suits.Length];
        var band = Bands[(index * 5) % Bands.Length];
        var eyes = (index % 3) switch
        {
            0 => """<circle cx="52" cy="55" r="4" fill="#1b1b1f"/><circle cx="76" cy="55" r="4" fill="#1b1b1f"/>""",
            1 => """<path d="M46 57 q6 -6 12 0 M70 57 q6 -6 12 0" stroke="#1b1b1f" stroke-width="3" fill="none" stroke-linecap="round"/>""",
            _ => """<path d="M46 52 l12 4 M82 52 l-12 4" stroke="#1b1b1f" stroke-width="3" stroke-linecap="round"/><circle cx="53" cy="58" r="3" fill="#1b1b1f"/><circle cx="75" cy="58" r="3" fill="#1b1b1f"/>""",
        };

        return string.Create(CultureInfo.InvariantCulture, $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" role="img" aria-label="Ninja avatar">
              <rect width="128" height="128" rx="24" fill="{background}"/>
              <path d="M22 128 C22 98 42 86 64 86 C86 86 106 98 106 128 Z" fill="{suit}"/>
              <rect x="32" y="110" width="64" height="9" rx="2" fill="{beltColor}" stroke="#00000022"/>
              <circle cx="64" cy="56" r="34" fill="{suit}"/>
              <rect x="36" y="45" width="56" height="20" rx="10" fill="#f2c9a0"/>
              {eyes}
              <rect x="30" y="33" width="68" height="8" rx="4" fill="{band}"/>
              <path d="M96 36 l18 -8 l-6 12 z M96 39 l16 4 l-10 6 z" fill="{band}"/>
            </svg>
            """);
    }
}
