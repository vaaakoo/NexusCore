using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nexus.Core.Infrastructure.Common.Models;

namespace Nexus.Core.Infrastructure.Middleware;

/// <summary>
/// ASP.NET Core middleware that acts as a global safety net for unhandled exceptions.
/// Every exception is caught here, logged with full context (TraceId, path, method),
/// and serialized to a consistent <see cref="Result"/> JSON envelope so that API
/// consumers always receive a predictable error contract.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    // ── Shared serializer options (allocate once) ──────────────────────────────
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId    = context.TraceIdentifier;
        var path       = context.Request.Path;
        var method     = context.Request.Method;
        var statusCode = ResolveStatusCode(exception);

        // Structured log with all relevant context so the error is fully
        // searchable in any centralised log sink (Seq, Elastic, Application Insights).
        logger.LogError(
            exception,
            "Unhandled exception | TraceId={TraceId} | {Method} {Path} | StatusCode={StatusCode} | Type={ExceptionType}",
            traceId,
            method,
            path,
            statusCode,
            exception.GetType().Name);

        var body = BuildResponseBody(exception, statusCode, traceId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = statusCode;

        // Prevent downstream middleware from caching error responses.
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma       = "no-cache";

        var json = JsonSerializer.Serialize(body, JsonOptions);
        await context.Response.WriteAsync(json, context.RequestAborted);
    }

    /// <summary>
    /// Maps well-known exception types to appropriate HTTP status codes.
    /// Add more mappings here as your domain grows (e.g. <c>UnauthorizedAccessException</c> → 401).
    /// </summary>
    private static int ResolveStatusCode(Exception exception) => exception switch
    {
        BadHttpRequestException or ArgumentNullException or ArgumentException or InvalidOperationException or ValidationException
            => (int)HttpStatusCode.BadRequest,

        UnauthorizedAccessException
            => (int)HttpStatusCode.Unauthorized,

        KeyNotFoundException
            => (int)HttpStatusCode.NotFound,

        NotImplementedException
            => (int)HttpStatusCode.NotImplemented,

        OperationCanceledException or TaskCanceledException
            => 499, // Client Closed Request (nginx convention)

        _ => (int)HttpStatusCode.InternalServerError
    };

    /// <summary>
    /// Builds the JSON body returned to the client.
    /// In non-development environments, the raw exception message is intentionally
    /// suppressed and replaced with a safe generic message to prevent information leakage.
    /// </summary>
    private static ErrorResponse BuildResponseBody(Exception exception, int statusCode, string traceId)
    {
        // Expose the real message only for client errors (4xx); mask server errors.
        var isSafeToExpose = statusCode is >= 400 and < 500
                             && exception is not OperationCanceledException
                             && exception is not TaskCanceledException;

        var message = isSafeToExpose
            ? exception.Message
            : "An unexpected error occurred. Please try again later or contact support.";

        return new ErrorResponse(
            IsSuccess:    false,
            StatusCode:   statusCode,
            ErrorMessage: message,
            TraceId:      traceId,
            Timestamp:    DateTimeOffset.UtcNow);
    }
}

/// <summary>
/// The JSON contract returned by <see cref="GlobalExceptionMiddleware"/> on any unhandled error.
/// Mirrors the shape of <see cref="Result"/> so consumers use a single deserialization model.
/// </summary>
internal sealed record ErrorResponse(
    bool            IsSuccess,
    int             StatusCode,
    string          ErrorMessage,
    string          TraceId,
    DateTimeOffset  Timestamp);

/// <summary>
/// Extension method for conveniently registering <see cref="GlobalExceptionMiddleware"/>
/// in the application pipeline.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds the Nexus global exception handler to the middleware pipeline.
    /// This should be the <b>first</b> middleware registered so it wraps all subsequent ones.
    /// </summary>
    public static IApplicationBuilder UseNexusExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();
}
