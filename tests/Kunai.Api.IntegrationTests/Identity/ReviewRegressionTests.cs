using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.Features.Catalog;
using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Identity;

/// <summary>Regression tests for problems found in the independent review.</summary>
public sealed class ReviewRegressionTests(KunaiApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cookie_session_survives_security_stamp_revalidation_and_a_password_change_on_the_same_device()
    {
        var user = await factory.RegisterConfirmedAsync();
        var browser = factory.CreateApiClient();
        await browser.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = user.Password, useCookie = true }, Ct);

        for (var i = 0; i < 3; i++)
        {
            (await browser.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/password")
        {
            Content = JsonContent.Create(new { currentPassword = user.Password, newPassword = "a fresh passphrase here" }),
        };
        change.Headers.Add("X-CSRF", "1");
        (await browser.SendAsync(change, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await browser.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await browser.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Locking_an_account_with_bad_logins_does_not_end_its_existing_sessions()
    {
        var user = await factory.RegisterConfirmedAsync();
        var tokens = await factory.LoginWithTokensAsync(user);
        var attacker = factory.CreateApiClient();
        for (var i = 0; i < 5; i++)
        {
            await attacker.PostAsJsonAsync("/api/v1/auth/login", new { login = user.Username, password = "guessing wrong!" }, Ct);
        }

        var refresh = await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }, Ct);

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Password_reset_recovers_an_unconfirmed_account()
    {
        var client = factory.CreateApiClient();
        var username = TestData.NewUsername();
        var email = TestData.EmailFor(username);
        await client.PostAsJsonAsync("/api/v1/auth/register", new { email, username, password = TestData.Password }, Ct);

        await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email }, Ct);
        var (userId, token) = factory.Emails.LastLink(email, "reset-password");
        (await client.PostAsJsonAsync("/api/v1/auth/password/reset", new { userId, token, newPassword = "recovered passphrase" }, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync("/api/v1/auth/login", new { login = username, password = "recovered passphrase" }, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unreviewed_manual_books_stay_off_public_shelves_and_profiles()
    {
        var user = await factory.RegisterConfirmedAsync();
        var owner = await factory.CreateSignedInClientAsync(user);
        var created = await owner.PostAsJsonAsync("/api/v1/items", new { title = "Spam " + Guid.NewGuid() }, Ct);
        var itemId = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString();
        await owner.PutAsJsonAsync($"/api/v1/me/shelf/{itemId}", new { status = "completed", isFavorite = true }, Ct);
        var anonymous = factory.CreateApiClient();

        (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}/shelf", Ct)).GetProperty("totalCount").GetInt32().Should().Be(0);
        var profile = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}", Ct);
        profile.GetProperty("favorites").GetArrayLength().Should().Be(0);
        profile.GetProperty("rank").GetProperty("points").GetInt32().Should().Be(0);
        (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/users/{user.Username}/shelf", Ct)).GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Someone_elses_pending_isbn_does_not_leak_its_id()
    {
        var isbn = "978" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        isbn += CheckDigit(isbn);
        var first = await factory.CreateSignedInClientAsync();
        var second = await factory.CreateSignedInClientAsync();
        await first.PostAsJsonAsync("/api/v1/items", new { title = "Original", isbn13 = isbn }, Ct);

        var duplicate = await second.PostAsJsonAsync("/api/v1/items", new { title = "Copy", isbn13 = isbn }, Ct);

        var problem = await SignInTests.ShouldBeProblem(duplicate, HttpStatusCode.Conflict, "book_pending_review");
        problem.GetProperty("detail").GetString().Should().NotContainAny("-");
    }

    [Theory]
    [InlineData("""{"status":2}""", null)]
    [InlineData("""{"status":"completed","finishedOn":"0001-01-01"}""", "finishedOn")]
    public async Task Shelf_rejects_integer_enums_and_absurd_dates(string body, string? field)
    {
        var client = await factory.CreateSignedInClientAsync();
        var bookId = await factory.CreateItemAsync(ItemType.Book);

        var response = await client.PutAsync($"/api/v1/me/shelf/{bookId}", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        if (field is not null)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            problem.GetProperty("errors").GetProperty(field)[0].GetString().Should().Be("year_out_of_range");
        }
    }

    [Fact]
    public async Task Status_filter_accepts_whole_names_only()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.GetAsync("/api/v1/me/shelf?status=planned,completed", Ct);

        (await SignInTests.ShouldBeProblem(response, HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors").GetProperty("status")[0].GetString().Should().Be("status_invalid");
    }

    [Fact]
    public async Task Settings_require_an_explicit_visibility()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/me/settings", new { locale = "tr", timeZone = "Europe/Istanbul", avatarId = "ninja-01" }, Ct);

        (await SignInTests.ShouldBeProblem(response, HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors").GetProperty("isProfilePublic")[0].GetString().Should().Be("required");
    }

    [Fact]
    public async Task OpenApi_describes_the_error_contract()
    {
        var document = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Ct);

        var problem = document.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");
        problem.GetProperty("properties").TryGetProperty("code", out _).Should().BeTrue();
        problem.GetProperty("properties").TryGetProperty("traceId", out _).Should().BeTrue();
        var me = document.GetProperty("paths").GetProperty("/api/v1/me").GetProperty("get").GetProperty("responses");
        me.TryGetProperty("401", out _).Should().BeTrue();
        me.TryGetProperty("429", out _).Should().BeTrue();
    }

    private static string CheckDigit(string first12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return ((10 - (sum % 10)) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
