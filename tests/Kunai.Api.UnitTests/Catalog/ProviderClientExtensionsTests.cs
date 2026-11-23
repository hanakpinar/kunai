using Kunai.Api.Features.Catalog.Providers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kunai.Api.UnitTests.Catalog;

public sealed class ProviderClientExtensionsTests
{
    [Fact]
    public void Provider_clients_work_without_resilience_overrides_in_configuration()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("provider").AddProviderResilience(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var create = () => provider.GetRequiredService<IHttpClientFactory>().CreateClient("provider");

        create.Should().NotThrow();
    }

    [Fact]
    public void Resilience_overrides_are_applied_when_present()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:Resilience:Retry:MaxRetryAttempts"] = "1" })
            .Build();
        var services = new ServiceCollection();
        services.AddHttpClient("provider").AddProviderResilience(configuration);
        using var provider = services.BuildServiceProvider();

        var create = () => provider.GetRequiredService<IHttpClientFactory>().CreateClient("provider");

        create.Should().NotThrow();
    }
}
