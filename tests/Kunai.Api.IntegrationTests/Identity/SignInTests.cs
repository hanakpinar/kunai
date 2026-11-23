using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Identity;

public sealed class SignInTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Mobile_login_returns_tokens_that_open_protected_endpoints()
    {
        var user = await factory.RegisterConfirmedAsync();
        var tokens = await factory.LoginWithTokensAsync(user);

        var me = await factory.CreateBearerClient(tokens.AccessToken).GetFromJsonAsync<JsonElement>("/api/v1/me", Ct);

        me.GetProperty("username").GetString().Should().Be(user.Username);
        me.GetProperty("email").GetString().Should().Be(user.Email);
    }

    [Fact]
    public async Task Login_accepts_the_email_address_too()
    {
        var user = await factory.RegisterConfirmedAsync();

        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { login = user.Email, password = user.Password }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Protected_endpoints_answer_401_problem_without_credentials()
    {
        var response = await factory.CreateApiClient().GetAsync("/api/v1/me", Ct);

        await ShouldBeProblem(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Wrong_password_is_rejected_and_five_failures_lock_the_account()
    {
        var user = await factory.RegisterConfirmedAsync();
        var client = factory.CreateApiClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = "wrong password!!" }, Ct);
            await ShouldBeProblem(wrong, HttpStatusCode.Unauthorized, "invalid_credentials");
        }

        var fifth = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = "wrong password!!" }, Ct);
        await ShouldBeProblem(fifth, HttpStatusCode.Forbidden, "account_locked");

        var correctButLocked = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = user.Password }, Ct);
        await ShouldBeProblem(correctButLocked, HttpStatusCode.Forbidden, "account_locked");
    }

    [Fact]
    public async Task Unconfirmed_email_cannot_sign_in()
    {
        var client = factory.CreateApiClient();
        var username = TestData.NewUsername();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { email = TestData.EmailFor(username), username, password = TestData.Password }, Ct);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = username, password = TestData.Password }, Ct);

        await ShouldBeProblem(response, HttpStatusCode.Forbidden, "email_not_confirmed");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reusing_an_old_one_ends_the_session()
    {
        var user = await factory.RegisterConfirmedAsync();
        var first = await factory.LoginWithTokensAsync(user);
        var client = factory.CreateApiClient();

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken }, Ct);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await refreshed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var secondAccess = second.GetProperty("accessToken").GetString()!;
        var secondRefresh = second.GetProperty("refreshToken").GetString()!;
        secondRefresh.Should().NotBe(first.RefreshToken);

        // Replaying the first token looks like theft: the whole session is revoked.
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken }, Ct);
        await ShouldBeProblem(replay, HttpStatusCode.Unauthorized, "invalid_refresh_token");

        var afterRevoke = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = secondRefresh }, Ct);
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateBearerClient(secondAccess).GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_makes_the_access_token_useless_immediately()
    {
        var user = await factory.RegisterConfirmedAsync();
        var tokens = await factory.LoginWithTokensAsync(user);
        var client = factory.CreateBearerClient(tokens.AccessToken);

        (await client.PostAsync("/api/v1/auth/logout", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var refresh = await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }, Ct);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_all_ends_every_device()
    {
        var user = await factory.RegisterConfirmedAsync();
        var phone = factory.CreateBearerClient((await factory.LoginWithTokensAsync(user)).AccessToken);
        var tablet = factory.CreateBearerClient((await factory.LoginWithTokensAsync(user)).AccessToken);

        (await phone.PostAsync("/api/v1/auth/logout-all", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await tablet.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        const string forged = "eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMTkzYjdlMi01YzFhLTdkM2UtOWYxMC0yYTRiNmM4ZDBlMTEiLCJzaWQiOiIwMTkzYjdlMi01YzFhLTdkM2UtOWYxMC0yYTRiNmM4ZDBlMTIiLCJpc3MiOiJrdW5haSIsImF1ZCI6Imt1bmFpLWFwaSIsImV4cCI6NDEwMjQ0NDgwMH0.c2lnbmF0dXJlLWZyb20tYW5vdGhlci1rZXktMTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI";

        var response = await factory.CreateBearerClient(forged).GetAsync("/api/v1/me", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    internal static async Task<JsonElement> ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().Should().Be(code);
        return problem;
    }
}
