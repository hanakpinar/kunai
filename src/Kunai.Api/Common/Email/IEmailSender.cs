namespace Kunai.Api.Common.Email;

/// <summary>Sends transactional email. Mailpit catches everything locally; a real provider is used when hosted.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
