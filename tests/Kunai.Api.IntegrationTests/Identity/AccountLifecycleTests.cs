using System.Net;
using System.Net.Http.Json;

using Kunai.Api.Features.Catalog;
using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Identity;

public sealed class AccountLifecycleTests(KunaiApiFactory factory)
{
    private const string NewPassword = "a brand new passphrase";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Password_reset_sets_a_new_password_and_signs_out_every_device()
    {
        var user = await factory.RegisterConfirmedAsync();
        var oldTokens = await factory.LoginWithTokensAsync(user);
        var anonymous = factory.CreateApiClient();

        (await anonymous.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email = user.Email }, Ct)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var (userId, token) = factory.Emails.LastLink(user.Email, "reset-password");

        var reset = await anonymous.PostAsJsonAsync("/api/v1/auth/password/reset", new { userId, token, newPassword = NewPassword }, Ct);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await factory.CreateBearerClient(oldTokens.AccessToken).GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = user.Password }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = NewPassword }, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reused = await anonymous.PostAsJsonAsync("/api/v1/auth/password/reset", new { userId, token, newPassword = "yet another passphrase" }, Ct);
        await SignInTests.ShouldBeProblem(reused, HttpStatusCode.BadRequest, "invalid_or_expired_token");
    }

    [Fact]
    public async Task Forgot_password_answers_202_for_unknown_emails_without_sending_anything()
    {
        var email = TestData.EmailFor(TestData.NewUsername());

        (await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/password/forgot", new { email }, Ct)).StatusCode.Should().Be(HttpStatusCode.Accepted);

        factory.Emails.SentTo(email).Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_device_and_signs_out_the_others()
    {
        var user = await factory.RegisterConfirmedAsync();
        var phone = factory.CreateBearerClient((await factory.LoginWithTokensAsync(user)).AccessToken);
        var laptop = factory.CreateBearerClient((await factory.LoginWithTokensAsync(user)).AccessToken);

        var wrong = await phone.PostAsJsonAsync("/api/v1/me/password", new { currentPassword = "not my password", newPassword = NewPassword }, Ct);
        (await SignInTests.ShouldBeProblem(wrong, HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors").GetProperty("currentPassword")[0].GetString().Should().Be("password_mismatch");

        (await phone.PostAsJsonAsync("/api/v1/me/password", new { currentPassword = user.Password, newPassword = NewPassword }, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await phone.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await laptop.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deleting_the_account_removes_the_user_and_their_shelf()
    {
        var user = await factory.RegisterConfirmedAsync();
        var client = await factory.CreateSignedInClientAsync(user);
        var bookId = await factory.CreateItemAsync(ItemType.Book);
        await client.PutAsJsonAsync($"/api/v1/me/shelf/{bookId}", new { status = "planned" }, Ct);

        using var wrong = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me") { Content = JsonContent.Create(new { password = "wrong password!" }) };
        (await client.SendAsync(wrong, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me") { Content = JsonContent.Create(new { password = user.Password }) };
        (await client.SendAsync(delete, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateApiClient().GetAsync($"/api/v1/users/{user.Username}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = user.Password }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateApiClient().GetAsync($"/api/v1/items/{bookId}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
