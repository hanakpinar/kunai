using Kunai.Web.Client.Infrastructure;

using Microsoft.Kiota.Abstractions;

namespace Kunai.Web.Client.Pages.Search;

/// <summary>
/// A 401 on a signed-in call means the session cookie has expired. Reloading the session turns the UI to its
/// signed-out state: the router sends an [Authorize] page to sign in, a public page shows its sign-in links.
/// </summary>
internal static class SessionExpiry
{
    public static async Task RefreshIfExpiredAsync(this AuthSession session, ApiProblem problem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(problem);
        if (problem.Status != 401)
        {
            return;
        }

        try
        {
            await session.RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            // GET /me could not answer either; the alert already asks to sign in again.
        }
    }
}
