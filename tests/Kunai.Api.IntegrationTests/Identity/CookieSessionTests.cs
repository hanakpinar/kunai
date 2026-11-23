using System.Net;
using System.Net.Http.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Identity;

public sealed class CookieSessionTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Web_login_sets_a_hardened_cookie_that_authenticates_requests()
    {
        var user = await factory.RegisterConfirmedAsync();
        var browser = factory.CreateApiClient();

        var login = await browser.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = user.Password, useCookie = true }, Ct);

        login.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-kunai=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=lax").And.Contain("path=/");
        (await browser.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cookie_writes_without_the_csrf_header_are_refused()
    {
        var user = await factory.RegisterConfirmedAsync();
        var browser = factory.CreateApiClient();
        await browser.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = user.Password, useCookie = true }, Ct);

        var withoutHeader = await browser.PostAsync("/api/v1/auth/logout", null, Ct);
        await SignInTests.ShouldBeProblem(withoutHeader, HttpStatusCode.Forbidden, "csrf_header_missing");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("X-CSRF", "1");
        (await browser.SendAsync(request, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await browser.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
