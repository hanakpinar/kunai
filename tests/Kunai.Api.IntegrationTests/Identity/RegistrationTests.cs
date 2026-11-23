using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Identity;

public sealed class RegistrationTests(KunaiApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_sends_a_confirmation_link_that_confirms_the_email()
    {
        var username = TestData.NewUsername();
        var email = TestData.EmailFor(username);

        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, username, password = TestData.Password, locale = "en" }, Ct);
        register.StatusCode.Should().Be(HttpStatusCode.Accepted);

        factory.Emails.SentTo(email).Should().ContainSingle().Which.Subject.Should().Be("Confirm your Kunai email");
        var (userId, token) = factory.Emails.LastLink(email, "confirm-email");

        var confirm = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, token }, Ct);
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Confirmation_with_a_tampered_token_is_rejected()
    {
        var username = TestData.NewUsername();
        var email = TestData.EmailFor(username);
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, username, password = TestData.Password }, Ct);
        var (userId, _) = factory.Emails.LastLink(email, "confirm-email");

        var confirm = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, token = "bm90LWEtdG9rZW4" }, Ct);

        await ShouldBeProblem(confirm, HttpStatusCode.BadRequest, "invalid_or_expired_token");
    }

    [Fact]
    public async Task Registering_an_existing_email_answers_the_same_and_warns_the_owner()
    {
        var username = TestData.NewUsername();
        var email = TestData.EmailFor(username);
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, username, password = TestData.Password }, Ct);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, username = TestData.NewUsername(), password = TestData.Password }, Ct);

        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        factory.Emails.SentTo(email).Should().HaveCount(2);
        factory.Emails.SentTo(email)[1].Subject.Should().Be("Zaten bir Kunai hesabın var");
    }

    [Fact]
    public async Task Taken_username_returns_409()
    {
        var username = TestData.NewUsername();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = TestData.EmailFor(username), username, password = TestData.Password }, Ct);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = TestData.EmailFor(TestData.NewUsername()), username = username.ToUpperInvariant(), password = TestData.Password }, Ct);

        await ShouldBeProblem(second, HttpStatusCode.Conflict, "username_taken");
    }

    [Theory]
    [InlineData("ab", "username_invalid")]
    [InlineData("1abc", "username_invalid")]
    [InlineData("çağrı", "username_invalid")]
    [InlineData("admin", "username_reserved")]
    public async Task Invalid_usernames_are_rejected_with_a_field_code(string username, string code)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = TestData.EmailFor(TestData.NewUsername()), username, password = TestData.Password }, Ct);

        var problem = await ShouldBeProblem(response, HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("errors").GetProperty("username")[0].GetString().Should().Be(code);
    }

    [Fact]
    public async Task Short_password_and_bad_email_are_rejected_by_validation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = "not-an-email", username = TestData.NewUsername(), password = "short" }, Ct);

        var problem = await ShouldBeProblem(response, HttpStatusCode.BadRequest, "validation_failed");
        var errors = problem.GetProperty("errors");
        errors.GetProperty("password")[0].GetString().Should().Be("password_length");
        errors.GetProperty("email")[0].GetString().Should().Be("email_invalid");
    }

    [Fact]
    public async Task Username_availability_reports_free_taken_and_invalid_names()
    {
        var username = TestData.NewUsername();
        (await Availability(username)).GetProperty("available").GetBoolean().Should().BeTrue();

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = TestData.EmailFor(username), username, password = TestData.Password }, Ct);

        var taken = await Availability(username);
        taken.GetProperty("available").GetBoolean().Should().BeFalse();
        taken.GetProperty("reason").GetString().Should().Be("username_taken");
        (await Availability("x")).GetProperty("reason").GetString().Should().Be("username_invalid");
    }

    [Fact]
    public async Task Resend_always_answers_202()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email/resend", new { email = "nobody@test.kunai" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private async Task<JsonElement> Availability(string username) =>
        await _client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/username-availability?username={username}", Ct);

    private static async Task<JsonElement> ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().Should().Be(code);
        return problem;
    }
}
