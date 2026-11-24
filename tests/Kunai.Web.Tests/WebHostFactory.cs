using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using WireMock.Server;

namespace Kunai.Web.Tests;

/// <summary>The web host with a WireMock server standing in for the API behind the /api proxy.</summary>
public sealed class WebHostFactory : WebApplicationFactory<Program>
{
    public WireMockServer Api { get; } = WireMockServer.Start();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Api:BaseUrl", Api.Url);
    }

    public override async ValueTask DisposeAsync()
    {
        Api.Stop();
        Api.Dispose();
        await base.DisposeAsync();
    }
}
