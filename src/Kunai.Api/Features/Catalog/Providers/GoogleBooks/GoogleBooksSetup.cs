using Microsoft.Extensions.Options;

namespace Kunai.Api.Features.Catalog.Providers.GoogleBooks;

public static class GoogleBooksSetup
{
    /// <summary>No client-side rate limit: Google's quota is per day, and a 429 already maps to provider_unavailable.</summary>
    public static IHostApplicationBuilder AddGoogleBooksProvider(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<GoogleBooksOptions>().Bind(builder.Configuration.GetSection(GoogleBooksOptions.SectionName));

        builder.Services.AddHttpClient<GoogleBooksProvider>((services, client) =>
                client.BaseAddress = services.GetRequiredService<IOptions<GoogleBooksOptions>>().Value.BaseUrl)
            .AddProviderResilience(builder.Configuration);

        builder.Services.AddTransient<IMetadataProvider>(services => services.GetRequiredService<GoogleBooksProvider>());
        return builder;
    }
}
