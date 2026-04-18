using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Nexus.Core.Infrastructure.Middleware;
using Nexus.Core.Tests.Common;
using Xunit;

namespace Nexus.Core.Tests.Middleware;

public sealed class GlobalExceptionMiddlewareTests : BaseTest
{
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly GlobalExceptionMiddleware _sut;

    public GlobalExceptionMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<GlobalExceptionMiddleware>>();
        _next   = Substitute.For<RequestDelegate>();
        _sut    = new GlobalExceptionMiddleware(_next, _logger);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCatchExceptionAndReturnStandardizedJson()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("Oops!");
        
        _next.Invoke(Arg.Any<HttpContext>()).Returns(x => throw exception);

        // Act
        await _sut.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400); // InvalidOperationException maps to 400
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("errorMessage").GetString().Should().Be("Oops!");
        json.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldLogExceptionWithContext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path   = "/test-path";
        context.Request.Method = "GET";
        context.TraceIdentifier = "test-trace-id";
        context.Response.Body   = new MemoryStream();

        var exception = new Exception("Server Error");
        _next.Invoke(Arg.Any<HttpContext>()).Returns(x => throw exception);

        // Act
        await _sut.InvokeAsync(context);

        // Assert
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("test-trace-id") && o.ToString()!.Contains("/test-path")),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
