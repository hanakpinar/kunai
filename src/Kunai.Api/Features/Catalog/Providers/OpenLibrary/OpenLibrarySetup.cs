using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

namespace Kunai.Api.Features.Catalog.Providers.OpenLibrary;

public static class OpenLibrarySetup
{
    public static IHostApplicationBuilder AddOpenLibraryProvider(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<OpenLibraryOptions>().Bind(builder.Configuration.GetSection(OpenLibraryOptions.SectionName));

        builder.Services.AddHttpClient<OpenLibraryProvider>((services, client) =>
            {
                client.BaseAddress = services.GetRequiredService<IOptions<OpenLibraryOptions>>().Value.BaseUrl;

                // Open Library asks API clients to identify themselves.
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kunai", "1.0"));
            })
            .AddProviderResilience(builder.Configuration)
            .AddProviderRateLimit("openlibrary", permitsPerMinute: 60);

        builder.Services.AddTransient<IMetadataProvider>(services => services.GetRequiredService<OpenLibraryProvider>());
        return builder;
    }
}
