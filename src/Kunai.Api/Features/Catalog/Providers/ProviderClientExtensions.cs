using Microsoft.Extensions.Http.Resilience;

namespace Kunai.Api.Features.Catalog.Providers;

public static class ProviderClientExtensions
{
    /// <summary>
    /// Retries, timeouts and a circuit breaker for one provider's HttpClient. Each provider gets its own
    /// pipeline (there is no global handler), tunable under "Providers:Resilience".
    /// </summary>
    /// <param name="builder">The provider's HttpClient.</param>
    /// <param name="configuration">App configuration; "Providers:Resilience" overrides apply when present.</param>
    /// <param name="configure">Provider-specific tuning applied on top of the shared defaults.</param>
    public static IHttpClientBuilder AddProviderResilience(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        Action<HttpStandardResilienceOptions>? configure = null)
    {
        var pipeline = builder.AddStandardResilienceHandler()
            .Configure(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.Retry.MaxRetryAttempts = 2;
                configure?.Invoke(options);
            });

        // Binding an absent section throws, so overrides are applied only when configured.
        var overrides = configuration.GetSection("Providers:Resilience");
        if (overrides.Exists())
        {
            pipeline.Configure(overrides);
        }

        return builder;
    }
}
