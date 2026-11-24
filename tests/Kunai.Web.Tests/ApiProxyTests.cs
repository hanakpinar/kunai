using System.Net;
using System.Net.Http.Headers;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Kunai.Web.Tests;

/// <summary>The browser calls /api on the web origin; the host forwards it to the API untouched.</summary>
public sealed class ApiProxyTests(WebHostFactory factory) : IClassFixture<WebHostFactory>
{
    [Fact]
    public async Task Api_calls_are_forwarded_with_the_session_cookie_and_csrf_header()
    {
        factory.Api.Given(Request.Create().WithPath("/api/v1/me/shelf/0192f8a4-7c1e-7d4a-9b8e-3f2a1c0d5e6f").UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"status":"inProgress"}"""));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/me/shelf/0192f8a4-7c1e-7d4a-9b8e-3f2a1c0d5e6f")
        {
            Content = new StringContent("""{"status":"inProgress"}""", new MediaTypeHeaderValue("application/json")),
        };
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add("Cookie", "__Host-kunai=session-value");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("inProgress");
        var forwarded = factory.Api.LogEntries.Should().ContainSingle(e => e.RequestMessage!.Method == "PUT").Which.RequestMessage!;
        forwarded.Headers!.Should().ContainKey("X-CSRF");
        forwarded.Cookies!.Should().Contain("__Host-kunai", "session-value");
        forwarded.Body.Should().Contain("inProgress");
    }

    [Fact]
    public async Task The_apis_cookie_reaches_the_browser_as_a_first_party_cookie()
    {
        factory.Api.Given(Request.Create().WithPath("/api/v1/auth/login").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NoContent)
                .WithHeader("Set-Cookie", "__Host-kunai=new-session; path=/; secure; samesite=lax; httponly"));
        using var client = factory.CreateClient(new() { HandleCookies = false });
        using var content = new StringContent("{}", new MediaTypeHeaderValue("application/json"));

        using var response = await client.PostAsync(new Uri("/api/v1/auth/login", UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Should().ContainSingle()
            .Which.Should().StartWith("__Host-kunai=new-session").And.NotContain("domain", "the cookie must stay bound to the web origin");
    }
}
