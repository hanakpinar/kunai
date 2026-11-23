using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

namespace Kunai.Api.Features.Catalog.Providers.AniList;

public static class AniListSetup
{
    public static IHostApplicationBuilder AddAniListProvider(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<AniListOptions>().Bind(builder.Configuration.GetSection(AniListOptions.SectionName));

        builder.Services.AddHttpClient<AniListProvider>((services, client) =>
            {
                client.BaseAddress = services.GetRequiredService<IOptions<AniListOptions>>().Value.BaseUrl;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddProviderResilience(builder.Configuration)
            .AddProviderRateLimit("anilist", permitsPerMinute: 30);

        builder.Services.AddTransient<IMetadataProvider>(services => services.GetRequiredService<AniListProvider>());
        return builder;
    }
}
