namespace Kunai.Api.Features.Catalog.Providers.GoogleBooks;

public sealed class GoogleBooksOptions
{
    public const string SectionName = "Providers:GoogleBooks";

    public Uri BaseUrl { get; set; } = new("https://www.googleapis.com/books/v1/");

    /// <summary>
    /// Google Cloud API key, required in practice: the anonymous quota is zero. Without a key the provider
    /// reports itself as not configured and book searches go straight to Open Library. Keep it in user-secrets.
    /// </summary>
    public string? ApiKey { get; set; }
}
