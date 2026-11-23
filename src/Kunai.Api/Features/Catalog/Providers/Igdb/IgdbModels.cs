namespace Kunai.Api.Features.Catalog.Providers.Igdb;

/// <summary>The parts of IGDB's and Twitch's JSON that Kunai reads (snake_case on the wire).</summary>
internal static class IgdbModels
{
    internal sealed record TokenResponse(string? AccessToken, long ExpiresIn, string? TokenType);

    internal sealed record Game(
        long Id,
        string? Name,
        long? FirstReleaseDate,
        string? Summary,
        Image? Cover,
        IReadOnlyList<Named>? Genres,
        IReadOnlyList<Platform>? Platforms,
        IReadOnlyList<InvolvedCompany>? InvolvedCompanies);

    internal sealed record Image(string? ImageId);

    internal sealed record Named(string? Name);

    internal sealed record Platform(string? Abbreviation, string? Name);

    internal sealed record InvolvedCompany(bool Developer, Named? Company);
}
