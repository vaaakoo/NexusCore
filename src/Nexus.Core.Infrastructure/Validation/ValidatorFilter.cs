using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Nexus.Core.Infrastructure.Common.Models;

namespace Nexus.Core.Infrastructure.Validation;

/// <summary>
/// A global filter for Minimal API endpoints that automatically discovers and
/// executes FluentValidation validators for the incoming request model.
/// If validation fails, it returns a standardised 400 Bad Request with the Result envelope.
/// </summary>
/// <typeparam name="T">The type of the request model to validate.</typeparam>
public sealed class ValidatorFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, 
        EndpointFilterDelegate           next)
    {
        // Find the argument of type T
        var request = context.Arguments.FirstOrDefault(arg => arg is T);

        if (request is not T validatableRequest)
            return await next(context);

        var validationResult = await validator.ValidateAsync(validatableRequest, context.HttpContext.RequestAborted);

        if (validationResult.IsValid)
            return await next(context);

        // Aggregate validation errors into a clean dictionary or bulleted list
        var errors = validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return TypedResults.Json(
            Result<IDictionary<string, string[]>>.Failure("Validation failed.", 400).Map(_ => errors), 
            statusCode: 400);
    }
}

public static class ValidatorFilterExtensions
{
    /// <summary>
    /// Adds automatic FluentValidation for the specified request type <typeparamref name="T"/>.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidatorFilter<T>>();
}
