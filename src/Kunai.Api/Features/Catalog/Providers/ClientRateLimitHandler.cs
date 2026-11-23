using System.Threading.RateLimiting;

namespace Kunai.Api.Features.Catalog.Providers;

/// <summary>
/// Keeps outgoing calls under a provider's published limit (e.g. AniList 30/min, MusicBrainz 1/s).
/// The limiter is a singleton per provider, so it survives HttpClient handler rotation.
/// </summary>
public sealed class ClientRateLimitHandler(RateLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var lease = await limiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new HttpRequestException("The client-side rate limit for this provider is exhausted.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

public static class ClientRateLimitExtensions
{
    /// <summary>Adds a token-bucket limit of <paramref name="permitsPerMinute"/> calls, queueing a few callers.</summary>
    public static IHttpClientBuilder AddProviderRateLimit(this IHttpClientBuilder builder, string key, int permitsPerMinute, int queueLimit = 10)
    {
        builder.Services.AddKeyedSingleton<RateLimiter>(key, (_, _) => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Max(1, permitsPerMinute / 6),
            TokensPerPeriod = Math.Max(1, permitsPerMinute / 6),
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
            QueueLimit = queueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        }));
        return builder.AddHttpMessageHandler(services => new ClientRateLimitHandler(services.GetRequiredKeyedService<RateLimiter>(key)));
    }
}
