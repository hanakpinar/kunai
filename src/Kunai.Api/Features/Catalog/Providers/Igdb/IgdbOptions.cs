namespace Kunai.Api.Features.Catalog.Providers.Igdb;

public sealed class IgdbOptions
{
    public const string SectionName = "Providers:Igdb";

    public Uri BaseUrl { get; set; } = new("https://api.igdb.com/v4/");

    /// <summary>Twitch OAuth endpoint that issues the app access token IGDB requires.</summary>
    public Uri TokenUrl { get; set; } = new("https://id.twitch.tv/oauth2/token");

    /// <summary>The Twitch application's Client ID. Keep it and the secret in user-secrets.</summary>
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public Uri ImageBaseUrl { get; set; } = new("https://images.igdb.com/igdb/image/upload/");
}
