using System.Data.Common;

namespace Kunai.Api.Common.Email;

public static class EmailSetup
{
    public static IHostApplicationBuilder AddKunaiEmail(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<EmailOptions>()
            .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
            .PostConfigure(options => options.SmtpEndpoint ??= EndpointFromConnectionString(builder.Configuration.GetConnectionString("mailpit")))
            .Validate(
                options => options.SmtpEndpoint is not null || builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"),
                "Email:SmtpEndpoint must be configured outside Development.")
            .ValidateOnStart();

        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return builder;
    }

    /// <summary>Aspire passes Mailpit as "Endpoint=smtp://localhost:PORT".</summary>
    private static Uri? EndpointFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var parsed = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return parsed.TryGetValue("Endpoint", out var endpoint) && Uri.TryCreate(endpoint?.ToString(), UriKind.Absolute, out var uri)
            ? uri
            : null;
    }
}
