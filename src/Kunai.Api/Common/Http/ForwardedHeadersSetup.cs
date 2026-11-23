using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

namespace Kunai.Api.Common.Http;

public static class ForwardedHeadersSetup
{
    /// <summary>
    /// Behind a proxy, the socket address is the proxy's. Only listed proxies may tell us the client's address,
    /// so per-IP rate limits see real clients and nobody can spoof a fresh address with a header.
    /// </summary>
    public static IServiceCollection AddKunaiForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var proxy = configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Unwrap every trusted hop (web proxy, ingress); stop at the first address that is not a known proxy.
            options.ForwardLimit = null;
            foreach (var address in proxy.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(address));
            }

            foreach (var network in proxy.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
        return services;
    }
}
