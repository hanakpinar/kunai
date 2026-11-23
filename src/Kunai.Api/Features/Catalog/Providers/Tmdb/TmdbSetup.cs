using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

namespace Kunai.Api.Features.Catalog.Providers.Tmdb;

public static class TmdbSetup
{
    public static IHostApplicationBuilder AddTmdbProvider(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<TmdbOptions>().Bind(builder.Configuration.GetSection(TmdbOptions.SectionName));

        builder.Services.AddHttpClient<TmdbProvider>((services, client) =>
            {
                var options = services.GetRequiredService<IOptions<TmdbOptions>>().Value;
                client.BaseAddress = options.BaseUrl;
                if (!string.IsNullOrWhiteSpace(options.AccessToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
                }
            })
            .AddProviderResilience(builder.Configuration);

        builder.Services.AddTransient<IMetadataProvider>(services => services.GetRequiredService<TmdbProvider>());
        return builder;
    }
}
