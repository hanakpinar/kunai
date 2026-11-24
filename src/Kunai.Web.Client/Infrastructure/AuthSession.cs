using System.Security.Claims;

using Kunai.Web.Client.Api;
using Kunai.Web.Client.Api.Models;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Kiota.Abstractions;

namespace Kunai.Web.Client.Infrastructure;

/// <summary>
/// Who is signed in. The session lives in the API's httpOnly cookie, which this code cannot read;
/// it asks GET /me instead and tells the UI when the answer changes.
/// </summary>
public sealed class AuthSession(KunaiApiClient api) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private Task<AuthenticationState>? _state;

    /// <summary>The signed-in user's account, or null.</summary>
    public MeResponse? Me { get; private set; }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    /// <summary>Signs in with the web cookie; throws the API's problem on failure.</summary>
    public async Task LoginAsync(string login, string password)
    {
        await api.Api.V1.Auth.Login.PostAsync(new LoginRequest { Login = login, Password = password, UseCookie = true, DeviceName = "Web" });
        await RefreshAsync();
    }

    public async Task LogoutAsync()
    {
        try
        {
            await api.Api.V1.Auth.Logout.PostAsync();
        }
        catch (ApiException)
        {
            // Already signed out on the server; clear the local state anyway.
        }

        await RefreshAsync();
    }

    /// <summary>Reloads the current user, e.g. after a profile or settings change.</summary>
    /// <remarks>
    /// Loads first and then notifies with a finished task. A pending task would make AuthorizeRouteView show its
    /// "authorizing" content for a moment, which disposes and recreates the current page and loses its state.
    /// </remarks>
    public async Task RefreshAsync()
    {
        var state = await LoadAsync();
        _state = Task.FromResult(state);
        NotifyAuthenticationStateChanged(_state);
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            Me = await api.Api.V1.Me.GetAsync();
        }
        catch (ApiException)
        {
            Me = null;
        }
        catch (HttpRequestException)
        {
            // The API is unreachable: keep whoever was signed in (anonymous on a cold start) instead of failing the router.
        }

        if (Me is null)
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Me.Id?.ToString() ?? string.Empty),
                new Claim(ClaimTypes.Name, Me.Username ?? string.Empty),
            ],
            authenticationType: "kunai");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
