using MailKit;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace Kunai.Api.Common.Email;

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        var endpoint = settings.SmtpEndpoint ?? throw new InvalidOperationException("Email:SmtpEndpoint is not configured.");

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        var security = string.Equals(endpoint.Scheme, "smtps", StringComparison.OrdinalIgnoreCase)
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(endpoint.Host, endpoint.Port, security, cancellationToken);

        if (!string.IsNullOrEmpty(settings.SmtpUsername))
        {
            await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(new SendRequest(mime), cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
