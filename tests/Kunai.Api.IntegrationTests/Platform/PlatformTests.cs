using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Kunai.Api.IntegrationTests.Infrastructure;

namespace Kunai.Api.IntegrationTests.Platform;

public sealed class PlatformTests(KunaiApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task Unknown_routes_answer_401_to_anonymous_callers_so_they_cannot_probe_the_api()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details_with_code_and_trace_id()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("code").GetString().Should().Be("not_found");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenApi_document_is_published_as_version_3_1()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);

        document.GetProperty("openapi").GetString().Should().StartWith("3.1");
        document.GetProperty("info").GetProperty("title").GetString().Should().Be("Kunai API");
    }

    [Fact]
    public async Task OpenApi_document_describes_both_sign_in_methods_on_protected_operations()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);

        var schemes = document.GetProperty("components").GetProperty("securitySchemes");
        schemes.GetProperty("Bearer").GetProperty("scheme").GetString().Should().Be("bearer");
        schemes.GetProperty("Cookie").GetProperty("name").GetString().Should().Be("__Host-kunai");

        var paths = document.GetProperty("paths");
        paths.GetProperty("/api/v1/me").GetProperty("get").TryGetProperty("security", out _).Should().BeTrue();
        paths.GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await _client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
    }
}
