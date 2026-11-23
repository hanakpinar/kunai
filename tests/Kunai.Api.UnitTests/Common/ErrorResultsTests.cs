using Kunai.Api.Common.Http;
using Kunai.Api.Common.Results;

namespace Kunai.Api.UnitTests.Common;

public sealed class ErrorResultsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.PreconditionFailed, 412)]
    [InlineData(ErrorType.BusinessRule, 422)]
    [InlineData(ErrorType.Unavailable, 503)]
    public void Each_error_type_maps_to_its_status_code(ErrorType type, int expected) =>
        ErrorResults.StatusCodeFor(type).Should().Be(expected);

    [Fact]
    public void Problem_carries_the_error_code()
    {
        var problem = Error.Conflict("username_taken", "The username is taken.").ToProblem();

        problem.StatusCode.Should().Be(409);
        problem.ProblemDetails.Extensions["code"].Should().Be("username_taken");
    }

    [Fact]
    public void Failed_result_has_no_value()
    {
        Result<int> result = Error.NotFound("item_not_found", "No such item.");

        result.IsSuccess.Should().BeFalse();
        var read = () => result.Value;
        read.Should().Throw<InvalidOperationException>();
    }
}
