using System.Text.Json.Serialization;

namespace Nexus.Core.Infrastructure.Common.Models;

/// <summary>
/// Represents the outcome of an operation that produces a value of type <typeparamref name="T"/>.
/// Follows the functional "Railway Oriented Programming" pattern to avoid throwing exceptions
/// for expected business failures.
/// </summary>
/// <typeparam name="T">The type of the value produced on success.</typeparam>
public sealed class Result<T>
{
    // ── Private constructor forces use of factory methods ─────────────────────
    private Result() { }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    [JsonIgnore]
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the value produced by the operation; <c>null</c> on failure.</summary>
    public T? Value { get; private init; }

    /// <summary>Gets a human-readable description of the failure; <c>null</c> on success.</summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>
    /// Gets the HTTP-equivalent status code that best describes the result.
    /// Defaults to <c>200</c> on success and <c>500</c> on failure.
    /// </summary>
    public int StatusCode { get; private init; }

    // ── Factory methods ────────────────────────────────────────────────────────

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    public static Result<T> Success(T value, int statusCode = 200) =>
        new() { IsSuccess = true, Value = value, StatusCode = statusCode };

    /// <summary>Creates a failed result with the supplied <paramref name="errorMessage"/>.</summary>
    public static Result<T> Failure(string errorMessage, int statusCode = 500) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, StatusCode = statusCode };

    // ── Fluent projection helpers ──────────────────────────────────────────────

    /// <summary>
    /// Transforms the value inside a successful result using <paramref name="map"/>.
    /// If the result is already a failure, the original failure is propagated unchanged.
    /// </summary>
    public Result<TNext> Map<TNext>(Func<T, TNext> map) =>
        IsSuccess
            ? Result<TNext>.Success(map(Value!), StatusCode)
            : Result<TNext>.Failure(ErrorMessage!, StatusCode);

    /// <summary>
    /// Chains two result-producing operations. If the current result is a failure,
    /// <paramref name="bind"/> is never called and the failure is propagated.
    /// </summary>
    public Result<TNext> Bind<TNext>(Func<T, Result<TNext>> bind) =>
        IsSuccess ? bind(Value!) : Result<TNext>.Failure(ErrorMessage!, StatusCode);

    /// <summary>Implicit conversion from <typeparamref name="T"/> to a successful result.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    public override string ToString() =>
        IsSuccess ? $"Success({Value})" : $"Failure({StatusCode}: {ErrorMessage})";
}

/// <summary>
/// Non-generic variant of <see cref="Result{T}"/> for operations that produce no value
/// (i.e., void / <see cref="Unit"/>-returning operations).
/// </summary>
public sealed class Result
{
    private Result() { }

    /// <inheritdoc cref="Result{T}.IsSuccess"/>
    public bool IsSuccess { get; private init; }

    /// <inheritdoc cref="Result{T}.IsFailure"/>
    [JsonIgnore]
    public bool IsFailure => !IsSuccess;

    /// <inheritdoc cref="Result{T}.ErrorMessage"/>
    public string? ErrorMessage { get; private init; }

    /// <inheritdoc cref="Result{T}.StatusCode"/>
    public int StatusCode { get; private init; }

    // ── Factory methods ────────────────────────────────────────────────────────

    /// <summary>Creates a successful result with no associated value.</summary>
    public static Result Success(int statusCode = 200) =>
        new() { IsSuccess = true, StatusCode = statusCode };

    /// <summary>Creates a failed result with the supplied <paramref name="errorMessage"/>.</summary>
    public static Result Failure(string errorMessage, int statusCode = 500) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, StatusCode = statusCode };

    // ── Convenience factory shortcuts ─────────────────────────────────────────

    /// <summary>Creates a 404 Not Found failure.</summary>
    public static Result NotFound(string errorMessage) => Failure(errorMessage, 404);

    /// <summary>Creates a 400 Bad Request failure.</summary>
    public static Result BadRequest(string errorMessage) => Failure(errorMessage, 400);

    /// <summary>Creates a 401 Unauthorized failure.</summary>
    public static Result Unauthorized(string errorMessage = "Unauthorized") => Failure(errorMessage, 401);

    /// <summary>Creates a 403 Forbidden failure.</summary>
    public static Result Forbidden(string errorMessage = "Forbidden") => Failure(errorMessage, 403);

    /// <summary>Creates a 409 Conflict failure.</summary>
    public static Result Conflict(string errorMessage) => Failure(errorMessage, 409);

    // ── Typed result helpers ───────────────────────────────────────────────────

    /// <summary>Wraps <paramref name="value"/> in a typed successful result.</summary>
    public static Result<T> Success<T>(T value, int statusCode = 200) =>
        Result<T>.Success(value, statusCode);

    /// <summary>Creates a typed failed result with the supplied <paramref name="errorMessage"/>.</summary>
    public static Result<T> Failure<T>(string errorMessage, int statusCode = 500) =>
        Result<T>.Failure(errorMessage, statusCode);

    /// <summary>Creates a typed 404 Not Found failure.</summary>
    public static Result<T> NotFound<T>(string errorMessage) =>
        Result<T>.Failure(errorMessage, 404);

    /// <summary>Creates a typed 400 Bad Request failure.</summary>
    public static Result<T> BadRequest<T>(string errorMessage) =>
        Result<T>.Failure(errorMessage, 400);

    public override string ToString() =>
        IsSuccess ? $"Success()" : $"Failure({StatusCode}: {ErrorMessage})";
}
