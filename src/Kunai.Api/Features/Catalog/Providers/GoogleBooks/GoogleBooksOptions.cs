namespace Kunai.Api.Features.Catalog.Providers.GoogleBooks;

public sealed class GoogleBooksOptions
{
    public const string SectionName = "Providers:GoogleBooks";

    public Uri BaseUrl { get; set; } = new("https://www.googleapis.com/books/v1/");

    /// <summary>
    /// Optional Google Cloud API key. Requests work without it, but then share Google's anonymous daily
    /// quota, which is often exhausted. Keep it in user-secrets.
    /// </summary>
    public string? ApiKey { get; set; }
}
