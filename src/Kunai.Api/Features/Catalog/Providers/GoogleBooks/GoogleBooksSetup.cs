using System.Net;

using Microsoft.Extensions.Http.Resilience;
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
            .AddProviderResilience(builder.Configuration, options =>
                // Google's 429 means the daily quota is used up; retrying only delays the fallback to Open Library.
                options.Retry.ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result?.StatusCode != HttpStatusCode.TooManyRequests
                    && HttpClientResiliencePredicates.IsTransient(args.Outcome)));

        builder.Services.AddTransient<IMetadataProvider>(services => services.GetRequiredService<GoogleBooksProvider>());
        return builder;
    }
}
