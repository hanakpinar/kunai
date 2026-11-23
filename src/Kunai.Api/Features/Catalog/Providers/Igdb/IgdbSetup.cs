using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Kunai.Api.Features.Catalog.Providers.Igdb;

public static class IgdbSetup
{
    public static IHostApplicationBuilder AddIgdbProvider(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<IgdbOptions>().Bind(builder.Configuration.GetSection(IgdbOptions.SectionName));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IgdbTokenSource>();
        builder.Services.AddTransient<IgdbAuthHandler>();

        builder.Services.AddHttpClient(IgdbTokenSource.ClientName, (services, client) =>
                client.BaseAddress = services.GetRequiredService<IOptions<IgdbOptions>>().Value.TokenUrl)
            .AddProviderResilience(builder.Configuration);

        // The auth handler sits outside the resilience pipeline: a token is fetched once per call, and its
        // 401 retry is not multiplied by the transient-fault retries.
        builder.Services.AddHttpClient<IgdbProvider>((services, client) =>
            {
                var options = services.GetRequiredService<IOptions<IgdbOptions>>().Value;
                client.BaseAddress = options.BaseUrl;
                if (!string.IsNullOrWhiteSpace(options.ClientId))
                {
                    client.DefaultRequestHeaders.Add("Client-ID", options.ClientId);
                }
            })
            .AddHttpMessageHandler<IgdbAuthHandler>()
            .AddProviderResilience(builder.Configuration)
            .AddProviderRateLimit("igdb", permitsPerMinute: 240);

        builder.Services.AddTransient<IMetadataProvider>(services => services.GetRequiredService<IgdbProvider>());
        return builder;
    }
}
