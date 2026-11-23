using System.Text;

using Kunai.Api.Common;
using Kunai.Api.Common.Email;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Kunai.Api.Features.Identity;

/// <summary>Builds account emails with links into the web app. A failed send is logged, never thrown.</summary>
internal sealed partial class IdentityEmails(
    UserManager<AppUser> users,
    IEmailSender sender,
    IOptions<AppOptions> app,
    ILogger<IdentityEmails> logger)
{
    public async Task SendConfirmationAsync(AppUser user, CancellationToken cancellationToken)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = Link("confirm-email", user.Id, token);
        var (subject, html, text) = IdentityEmailTemplates.ConfirmEmail(user.Locale, user.DisplayName, link);
        await SendAsync(user.Email!, subject, html, text, cancellationToken);
    }

    public async Task SendAccountExistsAsync(AppUser user, CancellationToken cancellationToken)
    {
        var link = new Uri(BaseUrl(), "forgot-password").ToString();
        var (subject, html, text) = IdentityEmailTemplates.AccountExists(user.Locale, link);
        await SendAsync(user.Email!, subject, html, text, cancellationToken);
    }

    public async Task SendPasswordResetAsync(AppUser user, CancellationToken cancellationToken)
    {
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = Link("reset-password", user.Id, token);
        var (subject, html, text) = IdentityEmailTemplates.PasswordReset(user.Locale, link);
        await SendAsync(user.Email!, subject, html, text, cancellationToken);
    }

    /// <summary>Identity tokens contain '+' and '/', so they travel base64url-encoded.</summary>
    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private string Link(string path, Guid userId, string token) =>
        new Uri(BaseUrl(), $"{path}?userId={userId}&token={EncodeToken(token)}").ToString();

    private Uri BaseUrl()
    {
        var url = app.Value.WebBaseUrl.ToString();
        return new Uri(url.EndsWith('/') ? url : url + "/");
    }

    private async Task SendAsync(string to, string subject, string html, string text, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(new EmailMessage(to, subject, html, text), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSendFailed(logger, exception, subject);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending email '{Subject}' failed")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, string subject);
}
