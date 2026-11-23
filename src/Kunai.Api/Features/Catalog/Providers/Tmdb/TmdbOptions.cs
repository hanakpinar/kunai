namespace Kunai.Api.Features.Catalog.Providers.Tmdb;

public sealed class TmdbOptions
{
    public const string SectionName = "Providers:Tmdb";

    public Uri BaseUrl { get; set; } = new("https://api.themoviedb.org/3/");

    public Uri ImageBaseUrl { get; set; } = new("https://image.tmdb.org/t/p/w500/");

    /// <summary>The "API Read Access Token" (v4) from the TMDB account settings. Keep it in user-secrets.</summary>
    public string? AccessToken { get; set; }

    public string Language { get; set; } = "tr-TR";

    public string FallbackLanguage { get; set; } = "en-US";
}
