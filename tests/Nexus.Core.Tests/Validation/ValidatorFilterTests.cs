using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Nexus.Core.Infrastructure.Validation;
using Nexus.Core.Tests.Common;
using Xunit;

namespace Nexus.Core.Tests.Validation;

public sealed class ValidatorFilterTests : BaseTest
{
    public sealed record TestRequest(string Name);

    [Fact]
    public async Task InvokeAsync_ShouldReturnNext_WhenValidationSucceeds()
    {
        // Arrange
        var request   = new TestRequest("Valid Name");
        var validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(request, Arg.Any<CancellationToken>())
                 .Returns(new ValidationResult());

        var filter = new ValidatorFilter<TestRequest>(validator);
        
        var context = Substitute.For<EndpointFilterInvocationContext>();
        context.Arguments.Returns(new List<object?> { request });
        
        var nextCalled = false;
        EndpointFilterDelegate next = _ => 
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        // Act
        await filter.InvokeAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnBadRequest_WhenValidationFails()
    {
        // Arrange
        var request   = new TestRequest("");
        var validator = Substitute.For<IValidator<TestRequest>>();
        var failures  = new List<ValidationFailure> { new("Name", "Name is required") };
        validator.ValidateAsync(request, Arg.Any<CancellationToken>())
                 .Returns(new ValidationResult(failures));

        var filter = new ValidatorFilter<TestRequest>(validator);
        
        var context = Substitute.For<EndpointFilterInvocationContext>();
        context.Arguments.Returns(new List<object?> { request });
        
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok());

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.Should().BeOfType<JsonHttpResult<Infrastructure.Common.Models.Result<Dictionary<string, string[]>>>>();
        var jsonResult = (JsonHttpResult<Infrastructure.Common.Models.Result<Dictionary<string, string[]>>>)result!;
        jsonResult.StatusCode.Should().Be(400);
        jsonResult.Value!.IsSuccess.Should().BeFalse();
        jsonResult.Value.ErrorMessage.Should().Be("Validation failed.");
    }
}
