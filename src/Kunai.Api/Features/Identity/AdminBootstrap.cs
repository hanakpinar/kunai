using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

/// <summary>
/// Grants the admin and moderator roles to the accounts listed in "Admin:Emails" at startup,
/// so the first admin does not need database access. Accounts must already exist.
/// </summary>
internal static partial class AdminBootstrap
{
    public static async Task GrantConfiguredAdminsAsync(this WebApplication app)
    {
        var emails = app.Configuration.GetSection("Admin:Emails").Get<string[]>() ?? [];
        if (emails.Length == 0)
        {
            return;
        }

        try
        {
            await GrantAsync(app, emails);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A database that is still starting must not crash-loop the API; the next start tries again.
            LogFailed(app.Logger, exception);
        }
    }

    private static async Task GrantAsync(WebApplication app, string[] emails)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var email in emails)
        {
            if (await users.FindByEmailAsync(email) is not { EmailConfirmed: true } user)
            {
                continue;
            }

            foreach (var role in new[] { Roles.Admin, Roles.Moderator })
            {
                if (!await users.IsInRoleAsync(user, role) && (await users.AddToRoleAsync(user, role)).Succeeded)
                {
                    LogGranted(app.Logger, role, user.Id);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Granting the configured admin roles failed; continuing startup")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Granted role {Role} to user {UserId}")]
    private static partial void LogGranted(ILogger logger, string role, Guid userId);
}
