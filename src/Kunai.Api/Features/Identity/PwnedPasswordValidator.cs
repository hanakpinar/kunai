using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

/// <summary>
/// Rejects breached passwords. Only the first 5 hex characters of the SHA-1 hash leave the server
/// (k-anonymity), and the check fails open: if the service is down, registration still works.
/// </summary>
internal sealed partial class PwnedPasswordValidator(HttpClient http, ILogger<PwnedPasswordValidator> logger)
    : IPasswordValidator<AppUser>
{
    public const string ErrorCode = "password_breached";

    public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return IdentityResult.Success;
        }

#pragma warning disable CA5350 // SHA-1 is what the Pwned Passwords range API is keyed on; it is not used for security here.
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350
        var prefix = hash[..5];
        var suffix = hash[5..];

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var body = await http.GetStringAsync(new Uri($"range/{prefix}", UriKind.Relative), timeout.Token);
            foreach (var line in body.Split('\n'))
            {
                var parts = line.Trim().Split(':');
                if (parts.Length == 2
                    && string.Equals(parts[0], suffix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                    && count > 0)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = ErrorCode,
                        Description = "This password appears in known data breaches. Choose another one.",
                    });
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogUnavailable(logger, exception);
        }

        return IdentityResult.Success;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pwned Passwords check skipped: the service is unavailable")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
