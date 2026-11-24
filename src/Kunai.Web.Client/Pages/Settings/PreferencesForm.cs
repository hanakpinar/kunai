namespace Kunai.Web.Client.Pages.Settings;

/// <summary>PUT /me/settings replaces all four values, so the form always carries every one.</summary>
public sealed class PreferencesForm
{
    /// <summary>Public unless the account says otherwise, like the API's default for new accounts.</summary>
    public bool IsProfilePublic { get; set; } = true;

    public string Locale { get; set; } = "tr";

    public string TimeZone { get; set; } = "Europe/Istanbul";

    public string AvatarId { get; set; } = "ninja-01";
}
