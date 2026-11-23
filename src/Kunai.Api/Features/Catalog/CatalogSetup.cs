using Kunai.Api.Features.Catalog.Providers;

namespace Kunai.Api.Features.Catalog;

public static class CatalogSetup
{
    public static IHostApplicationBuilder AddKunaiCatalog(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHybridCache();
        builder.AddKunaiProviders();
        return builder;
    }
}
