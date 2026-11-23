namespace Kunai.Api.Features.Catalog.Providers.OpenLibrary;

public sealed class OpenLibraryOptions
{
    public const string SectionName = "Providers:OpenLibrary";

    public Uri BaseUrl { get; set; } = new("https://openlibrary.org/");

    /// <summary>Cover images by cover id; unlike other cover lookups these URLs are not rate limited.</summary>
    public Uri CoversBaseUrl { get; set; } = new("https://covers.openlibrary.org/");
}
