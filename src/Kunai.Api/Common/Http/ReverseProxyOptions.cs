namespace Kunai.Api.Common.Http;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>Addresses of proxies allowed to set X-Forwarded-For (loopback is always trusted).</summary>
    public IReadOnlyList<string> KnownProxies { get; init; } = [];

    /// <summary>CIDR ranges of trusted proxies, e.g. the ingress subnet: "10.0.0.0/16".</summary>
    public IReadOnlyList<string> KnownNetworks { get; init; } = [];
}
