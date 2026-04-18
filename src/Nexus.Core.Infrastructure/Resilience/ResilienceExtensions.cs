using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System;

namespace Nexus.Core.Infrastructure.Resilience;

/// <summary>
/// Provides extension methods for configuring standard resilience patterns using Polly v8.
/// </summary>
public static class ResilienceExtensions
{
    public const string StandardPipeline = "nexus-standard";

    /// <summary>
    /// Registers a standard resilience pipeline that includes retry, circuit breaker, and timeout strategies.
    /// </summary>
    public static IServiceCollection AddNexusResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline(StandardPipeline, builder =>
        {
            builder
                // 1. Timeout: Outermost layer to enforce strict SLA
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(15),
                    Name = "NexusTimeout"
                })
                // 2. Retry: Exponential backoff with jitter for transient failures
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(1),
                    Name = "NexusRetry"
                })
                // 3. Circuit Breaker: Protecting downstream systems from overload
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(15),
                    Name = "NexusCircuitBreaker"
                });
        });

        return services;
    }
}
