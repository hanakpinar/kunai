using System.Net;

using Bunit;

using Kunai.Web.Client.Api;
using Kunai.Web.Client.Infrastructure;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Bundle;

namespace Kunai.Web.Client.Tests.Infrastructure;

/// <summary>
/// A bUnit context wired like the real app (generated client, session, catalog metadata) on top of
/// <see cref="FakeApi"/>. Item types are stubbed with the real labels; sign in with <see cref="SignInAs"/>.
/// </summary>
public abstract class KunaiTestContext : BunitContext
{
    protected KunaiTestContext()
    {
        Api = new FakeApi();
        Api.On(HttpMethod.Get, "/api/v1/item-types", TestCatalog.ItemTypes);
        Api.OnProblem(HttpMethod.Get, "/api/v1/me", HttpStatusCode.Unauthorized, "unauthorized");

        var http = new HttpClient(new CsrfHandler { InnerHandler = Api }) { BaseAddress = new Uri("https://kunai.test/") };
        Services.AddSingleton(http);
        Services.AddSingleton(new KunaiApiClient(new DefaultRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http) { BaseUrl = "https://kunai.test" }));
        Services.AddSingleton<AuthSession>();
        Services.AddSingleton<AuthenticationStateProvider>(services => services.GetRequiredService<AuthSession>());
        Services.AddSingleton<CatalogMetadata>();
        Services.AddAuthorizationCore();
        Services.AddCascadingAuthenticationState();
    }

    protected FakeApi Api { get; }

    /// <summary>Makes GET /me answer as this user, like a browser that holds a session cookie.</summary>
    protected void SignInAs(string username, string displayName = "Test Ninja") =>
        Api.On(HttpMethod.Get, "/api/v1/me", new
        {
            id = Guid.NewGuid(),
            username,
            email = $"{username}@test.kunai",
            displayName,
            avatarId = "ninja-03",
            isProfilePublic = true,
            locale = "tr",
            timeZone = "Europe/Istanbul",
            createdAt = DateTimeOffset.UtcNow,
        });
}
