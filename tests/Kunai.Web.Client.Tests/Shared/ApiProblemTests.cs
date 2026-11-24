using Kunai.Web.Client.Api.Models;
using Kunai.Web.Client.Infrastructure;

namespace Kunai.Web.Client.Tests.Shared;

public sealed class ApiProblemTests
{
    [Theory]
    [InlineData(503, "provider_unavailable", true)]
    [InlineData(503, "provider_not_configured", false)]
    [InlineData(500, "internal_error", true)]
    [InlineData(429, "rate_limited", true)]
    [InlineData(409, "book_exists", false)]
    [InlineData(401, "unauthorized", false)]
    public void Only_failures_that_can_pass_on_a_second_try_are_retryable(int status, string code, bool retryable)
    {
        var problem = ApiProblem.From(new ProblemDetails { Code = code, ResponseStatusCode = status });

        problem.IsRetryable.Should().Be(retryable);
        problem.Message.Should().Be(ErrorMessages.For(code)).And.NotBe(ErrorMessages.For("no_such_code"));
    }

    [Fact]
    public void A_network_failure_or_timeout_is_a_retryable_network_problem()
    {
        ApiProblem.From(new HttpRequestException("offline")).Should().Match<ApiProblem>(p => p.Code == "network" && p.IsRetryable);
        ApiProblem.From(new TaskCanceledException("timeout")).Should().Match<ApiProblem>(p => p.Code == "network" && p.IsRetryable);
    }

    [Fact]
    public void The_detail_is_kept_for_pages_that_need_it()
    {
        var problem = ApiProblem.From(new ProblemDetails { Code = "book_exists", ResponseStatusCode = 409, Detail = "0192f8a4-7c1e-7d4a-9b8e-3f2a1c0d5e6f" });

        problem.Detail.Should().Be("0192f8a4-7c1e-7d4a-9b8e-3f2a1c0d5e6f");
    }
}
