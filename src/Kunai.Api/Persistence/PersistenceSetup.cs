using Kunai.Api.Common.Persistence;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Kunai.Api.Persistence;

public static partial class PersistenceSetup
{
    /// <summary>The connection string name; Aspire injects it as ConnectionStrings__kunaidb.</summary>
    public const string ConnectionStringName = "kunaidb";

    public static IHostApplicationBuilder AddKunaiPersistence(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        builder.Services.AddSingleton<AuditInterceptor>();
        builder.Services.AddDbContext<AppDbContext>((services, options) =>
        {
            Configure(options, connectionString);
            options.AddInterceptors(services.GetRequiredService<AuditInterceptor>());
        });

        // Readiness fails when the database is unreachable; liveness does not.
        builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddNpgsql());

        builder.Services.AddDataProtection()
            .SetApplicationName("kunai")
            .PersistKeysToDbContext<AppDbContext>();

        return builder;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

    /// <summary>
    /// Applies pending migrations at startup, only in Development and Testing.
    /// Hosted environments run a migration bundle from CI before the new version starts.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        LogMigrated(app.Logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    private static partial void LogMigrated(ILogger logger);
}
