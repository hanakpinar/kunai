using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace Kunai.Api.Common.Http;

/// <summary>Named rate-limit policies. Every request also passes a global per-user or per-IP limit.</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Refresh = "refresh";
    public const string Search = "search";
    public const string Import = "import";

    public static IServiceCollection AddKunaiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = OnRejectedAsync;

            if (!options.Enabled)
            {
                foreach (var name in new[] { Auth, Refresh, Search, Import })
                {
                    limiter.AddPolicy(name, _ => RateLimitPartition.GetNoLimiter("disabled"));
                }

                return;
            }

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                var userId = UserId(context);
                return userId is not null
                    ? PerMinute("user:" + userId, options.AuthenticatedPerMinute)
                    : PerMinute("ip:" + ClientIp(context), options.AnonymousPerMinute);
            });

            limiter.AddPolicy(Auth, context => PerMinute("auth:" + ClientIp(context), options.AuthPerMinute));
            limiter.AddPolicy(Refresh, context => PerMinute("refresh:" + ClientIp(context), options.RefreshPerMinute));
            limiter.AddPolicy(Search, context => PerMinute("search:" + (UserId(context) ?? ClientIp(context)), options.SearchPerMinute));
            limiter.AddPolicy(Import, context => PerMinute("import:" + (UserId(context) ?? ClientIp(context)), options.ImportPerMinute));
        });

        return services;
    }

    private static RateLimitPartition<string> PerMinute(string key, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    private static string? UserId(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true ? context.User.FindFirstValue("sub") : null;

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Detail = "Slow down and retry after the number of seconds in the Retry-After header.",
            },
        });
    }
}
