using Kunai.Api.Features.Catalog.Providers.AniList;
using Kunai.Api.Features.Catalog.Providers.GoogleBooks;
using Kunai.Api.Features.Catalog.Providers.Igdb;
using Kunai.Api.Features.Catalog.Providers.OpenLibrary;
using Kunai.Api.Features.Catalog.Providers.Tmdb;

namespace Kunai.Api.Features.Catalog.Providers;

public static class ProvidersSetup
{
    public static IHostApplicationBuilder AddKunaiProviders(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<MetadataProviders>();

        builder.AddTmdbProvider();
        builder.AddGoogleBooksProvider();
        builder.AddOpenLibraryProvider();
        builder.AddIgdbProvider();
        builder.AddAniListProvider();

        return builder;
    }
}
