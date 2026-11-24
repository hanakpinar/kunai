using Kunai.Web.Client.Api;
using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

using Microsoft.AspNetCore.Components;
using Microsoft.Kiota.Abstractions;

namespace Kunai.Web.Client.Pages.Settings;

/// <summary>
/// One card on the settings page: its own request, busy flag, error and success message. After a
/// successful change the session is reloaded, so the header (name, profile link) follows.
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
    protected Task<bool> SaveAsync(Func<Task<MeResponse?>> request, string success) =>
        SaveAsync(
            async () =>
            {
                if (await request() is { } me)
                {
                    await MeChanged.InvokeAsync(me);
                }
            },
            success);

    /// <summary>Sends the change; on success shows <paramref name="success"/> and reloads the session.</summary>
    protected async Task<bool> SaveAsync(Func<Task> request, string success)
    {
        if (!await SendAsync(request))
        {
            return false;
        }

        Success = success;
        try
        {
            await Session.RefreshAsync();
        }
        catch (HttpRequestException)
        {
            // The change is saved; the header catches up on the next page load.
        }

        return true;
    }

    /// <summary>Sends one request with the busy flag and error handling; true when it succeeded.</summary>
    protected async Task<bool> SendAsync(Func<Task> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Busy = true;
        Problem = null;
        Success = null;
        try
        {
            await request();
            return true;
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            Problem = ApiProblem.From(exception);
            return false;
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>The problem for the card's alert, unless every field message already shows next to its input.</summary>
    protected ApiProblem? AlertProblem(params string[] inlineFields) =>
        Problem is { Fields.Count: > 0 } problem && problem.Fields.Keys.All(field => inlineFields.Contains(field, StringComparer.Ordinal))
            ? null
            : Problem;
}
