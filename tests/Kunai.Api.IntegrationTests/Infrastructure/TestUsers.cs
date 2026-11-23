using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kunai.Api.IntegrationTests.Infrastructure;

/// <summary>Creates confirmed users and signed-in clients in a few lines.</summary>
public static class TestUsers
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<TestUser> RegisterConfirmedAsync(this KunaiApiFactory factory)
    {
        var client = factory.CreateApiClient();
        var username = TestData.NewUsername();
        var user = new TestUser(username, TestData.EmailFor(username), TestData.Password);

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = user.Email, username, password = user.Password }, Ct);
        register.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var (userId, token) = factory.Emails.LastLink(user.Email, "confirm-email");
        var confirm = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, token }, Ct);
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return user;
    }

    public static async Task<Tokens> LoginWithTokensAsync(this KunaiApiFactory factory, TestUser user)
    {
        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = user.Password }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return new Tokens(body.GetProperty("accessToken").GetString()!, body.GetProperty("refreshToken").GetString()!);
    }

    /// <summary>A client that sends the user's bearer token, like the mobile app.</summary>
    public static async Task<HttpClient> CreateSignedInClientAsync(this KunaiApiFactory factory, TestUser? user = null)
    {
        user ??= await factory.RegisterConfirmedAsync();
        var tokens = await factory.LoginWithTokensAsync(user);
        return factory.CreateBearerClient(tokens.AccessToken);
    }

    public static HttpClient CreateBearerClient(this KunaiApiFactory factory, string accessToken)
    {
        var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
