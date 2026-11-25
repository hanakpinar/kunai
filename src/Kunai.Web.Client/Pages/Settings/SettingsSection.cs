using Kunai.Web.Client.Api;
using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

using Microsoft.AspNetCore.Components;

namespace Kunai.Web.Client.Pages.Settings;

/// <summary>
/// One card on the settings page: its own request, busy flag, error and success message. After a
/// successful change the session is reloaded, so the header (name, profile link) follows. When the
/// API says the session has ended, the session is reloaded too, so the router sends the user to sign in.
/// </summary>
public abstract class SettingsSection : ComponentBase
{
    [Inject]
    protected KunaiApiClient Api { get; set; } = null!;

    [Inject]
    protected AuthSession Session { get; set; } = null!;

    /// <summary>The account as last loaded or saved.</summary>
    [Parameter, EditorRequired]
    public MeResponse Me { get; set; } = null!;

    /// <summary>Raised with the account the API answered after a change (supports <c>@bind-Me</c>).</summary>
    [Parameter]
    public EventCallback<MeResponse> MeChanged { get; set; }

    protected bool Busy { get; private set; }

    protected ApiProblem? Problem { get; private set; }

    protected string? Success { get; private set; }

    /// <summary>Saves a change the API answers with the updated account.</summary>
    /// <returns>The account as the API stored it, or null when the save failed.</returns>
    protected async Task<MeResponse?> SaveAsync(Func<Task<MeResponse?>> request, string success)
    {
        ArgumentNullException.ThrowIfNull(request);
        MeResponse? saved = null;
        await SaveAsync(
            async () =>
            {
                saved = await request();
                if (saved is not null)
                {
                    await MeChanged.InvokeAsync(saved);
                }
            },
            success);
        return saved;
    }

    /// <summary>Sends the change; on success shows <paramref name="success"/> and reloads the session.</summary>
    protected async Task<bool> SaveAsync(Func<Task> request, string success)
    {
        if (!await SendAsync(request))
        {
            return false;
        }

        Success = success;

        // A lost connection or a timeout keeps the current user (AuthSession does not throw for them): the header
        // catches up on the next page load.
        await Session.RefreshAsync();
        return true;
    }

    /// <summary>Sends one request with the busy flag and error handling; true when it succeeded.</summary>
    protected async Task<bool> SendAsync(Func<Task> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Busy = true;
        Problem = null;
        Success = null;
        ApiProblem? problem = null;
        try
        {
            await request();
        }
        catch (Exception exception) when (ApiProblem.IsApiFailure(exception))
        {
            problem = ApiProblem.From(exception);
        }
        finally
        {
            Busy = false;
        }

        if (problem is null)
        {
            return true;
        }

        Problem = problem;

        // On a 401 (signed out in another tab, expired): once the session knows, the router asks for a sign-in.
        await Session.RefreshIfExpiredAsync(problem);
        return false;
    }

    /// <summary>The problem for the card's alert, unless every field message already shows next to its input.</summary>
    protected ApiProblem? AlertProblem(params string[] inlineFields) =>
        Problem is { Fields.Count: > 0 } problem && problem.Fields.Keys.All(field => inlineFields.Contains(field, StringComparer.Ordinal))
            ? null
            : Problem;
}
