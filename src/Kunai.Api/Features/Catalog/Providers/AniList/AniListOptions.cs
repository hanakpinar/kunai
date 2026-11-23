namespace Kunai.Api.Features.Catalog.Providers.AniList;

public sealed class AniListOptions
{
    public const string SectionName = "Providers:AniList";

    /// <summary>The GraphQL endpoint; every request is a POST to this address. No key is needed.</summary>
    public Uri BaseUrl { get; set; } = new("https://graphql.anilist.co/");
}
