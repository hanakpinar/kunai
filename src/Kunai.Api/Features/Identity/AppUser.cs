using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

/// <summary>A Kunai account. Identity handles credentials; the rest is the public profile and preferences.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 50;
    public const int BioMaxLength = 300;

    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string AvatarId { get; set; } = "ninja-01";

    public bool IsProfilePublic { get; set; } = true;

    /// <summary>UI and email language: "tr" or "en".</summary>
    public string Locale { get; set; } = "tr";

    /// <summary>IANA time zone used for "this year" in stats and goals.</summary>
    public string TimeZone { get; set; } = "Europe/Istanbul";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UsernameChangedAt { get; set; }
}
