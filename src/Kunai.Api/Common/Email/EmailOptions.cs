namespace Kunai.Api.Common.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@kunai.local";

    public string FromName { get; set; } = "Kunai";

    /// <summary>SMTP endpoint, e.g. smtp://localhost:1025. Falls back to the "mailpit" connection string from Aspire.</summary>
    public Uri? SmtpEndpoint { get; set; }

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }
}
