using Microsoft.Extensions.Http.Resilience;

namespace Kunai.Api.Features.Catalog.Providers;

public static class ProviderClientExtensions
{
    /// <summary>
    /// Retries, timeouts and a circuit breaker for one provider's HttpClient. Each provider gets its own
    /// pipeline (there is no global handler), tunable under "Providers:Resilience".
    /// </summary>
    public static IHttpClientBuilder AddProviderResilience(this IHttpClientBuilder builder, IConfiguration configuration)
    {
        builder.AddStandardResilienceHandler()
            .Configure(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.Retry.MaxRetryAttempts = 2;
            })
            .Configure(configuration.GetSection("Providers:Resilience"));
        return builder;
    }
}
