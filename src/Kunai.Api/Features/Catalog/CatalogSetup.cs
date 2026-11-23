using Kunai.Api.Features.Catalog.Providers;

namespace Kunai.Api.Features.Catalog;

public static class CatalogSetup
{
    public static IHostApplicationBuilder AddKunaiCatalog(this IHostApplicationBuilder builder)
    {
        // HybridCache keeps its local copies in this cache; the size limit bounds memory however many distinct
        // searches users type. Entries are sized by payload bytes.
        builder.Services.AddMemoryCache(options => options.SizeLimit = 64 * 1024 * 1024);
        builder.Services.AddHybridCache(options => options.MaximumPayloadBytes = 512 * 1024);
        builder.AddKunaiProviders();
        return builder;
    }
}
