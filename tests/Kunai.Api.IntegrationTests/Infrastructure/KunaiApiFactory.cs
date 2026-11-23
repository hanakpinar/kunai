using Kunai.Api.Common.Email;
using Kunai.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Testcontainers.PostgreSql;

using WireMock.Server;

[assembly: AssemblyFixture(typeof(KunaiApiFactory))]

namespace Kunai.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One API instance and one real PostgreSQL container shared by every test in the assembly.
/// Tests stay independent by creating their own users and data instead of resetting the database.
/// </summary>
public sealed class KunaiApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6")
        .WithEnvironment("POSTGRES_INITDB_ARGS", "--encoding=UTF8 --locale=C.UTF-8")
        .Build();

    public FakeEmailSender Emails { get; } = new();

    /// <summary>Stands in for TMDB, Google Books, Open Library, IGDB, Twitch and AniList. Paths are prefixed per provider.</summary>
    public WireMockServer Providers { get; } = WireMockServer.Start();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken);

        // Start the app (and run migrations) once, before tests create clients in parallel.
        _ = Server;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        Providers.Stop();
        Providers.Dispose();
    }

    /// <summary>An HTTPS client, so Secure cookies round-trip like in a browser.</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:kunaidb", _postgres.GetConnectionString());
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("Security:PwnedPasswords:Enabled", "false");

        var providers = Providers.Url!;
        builder.UseSetting("Providers:Resilience:Retry:MaxRetryAttempts", "1");
        builder.UseSetting("Providers:Resilience:Retry:Delay", "00:00:00.010");
        builder.UseSetting("Providers:Resilience:CircuitBreaker:MinimumThroughput", "1000");
        builder.UseSetting("Providers:Tmdb:BaseUrl", $"{providers}/tmdb/3/");
        builder.UseSetting("Providers:Tmdb:AccessToken", "test-token");
        builder.UseSetting("Providers:GoogleBooks:BaseUrl", $"{providers}/gbooks/v1/");
        builder.UseSetting("Providers:OpenLibrary:BaseUrl", $"{providers}/olib/");
        builder.UseSetting("Providers:Igdb:BaseUrl", $"{providers}/igdb/v4/");
        builder.UseSetting("Providers:Igdb:TokenUrl", $"{providers}/twitch/oauth2/token");
        builder.UseSetting("Providers:Igdb:ClientId", "test-client");
        builder.UseSetting("Providers:Igdb:ClientSecret", "test-secret");
        builder.UseSetting("Providers:AniList:BaseUrl", $"{providers}/anilist/");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}
