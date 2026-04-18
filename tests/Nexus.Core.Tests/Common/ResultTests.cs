using FluentAssertions;
using Nexus.Core.Infrastructure.Common.Models;
using Xunit;

namespace Nexus.Core.Tests.Common;

public sealed class ResultTests : BaseTest
{
    [Fact]
    public void Success_ShouldHoldValueAndBeSuccessful()
    {
        // Arrange
        var val = "test-value";

        // Act
        var result = Result<string>.Success(val);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(val);
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public void Failure_ShouldHoldErrorAndBeFailure()
    {
        // Arrange
        var error = "error-message";
        var status = 404;

        // Act
        var result = Result<string>.Failure(error, status);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be(error);
        result.StatusCode.Should().Be(status);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Map_ShouldTransformValue_WhenSuccess()
    {
        // Arrange
        var result = Result<int>.Success(10);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(20);
    }

    [Fact]
    public void Map_ShouldPropagateFailure_WhenFailure()
    {
        // Arrange
        var result = Result<int>.Failure("fail", 400);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeFalse();
        mapped.ErrorMessage.Should().Be("fail");
        mapped.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Bind_ShouldChainOperations_WhenSuccess()
    {
        // Arrange
        var result = Result<string>.Success("start");

        // Act
        var chained = result.Bind(s => Result<int>.Success(s.Length));

        // Assert
        chained.IsSuccess.Should().BeTrue();
        chained.Value.Should().Be(5);
    }

    [Fact]
    public void Bind_ShouldStopAndPropagate_WhenFirstIsFailure()
    {
        // Arrange
        var result = Result<string>.Failure("initial-fail", 401);
        var wasCalled = false;

        // Act
        var chained = result.Bind<int>(s => 
        {
            wasCalled = true;
            return Result<int>.Success(s.Length);
        });

        // Assert
        chained.IsSuccess.Should().BeFalse();
        chained.StatusCode.Should().Be(401);
        wasCalled.Should().BeFalse();
    }

    [Fact]
    public void ImplicitOperator_ShouldCreateResult()
    {
        // Act
        Result<string> result = "implicit";

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("implicit");
    }

    [Fact]
    public void NonGenericResult_Success_ShouldHave200()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public void NonGenericResult_Failure_ShouldHaveStatus()
    {
        // Act
        var result = Result.BadRequest("bad");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.ErrorMessage.Should().Be("bad");
    }
}
