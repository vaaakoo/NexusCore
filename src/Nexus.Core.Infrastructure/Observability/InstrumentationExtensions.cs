using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nexus.Core.Infrastructure.Observability;

/// <summary>
/// Provides extension methods for standardizing observability (Tracing and Metrics)
/// across the application using OpenTelemetry.
/// </summary>
public static class InstrumentationExtensions
{
    /// <summary>
    /// Configures OpenTelemetry Tracing and Metrics with standard instrumentation.
    /// </summary>
    public static IServiceCollection AddNexusObservability(
        this IServiceCollection services,
        IHostEnvironment        environment)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(environment.ApplicationName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment.EnvironmentName,
                ["machine.name"]           = Environment.MachineName
            });

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource(environment.ApplicationName);

                if (environment.IsDevelopment())
                {
                    tracing.AddConsoleExporter();
                }
                
                // Note: OTLP Exporter can be added here via configuration
                tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();
            });

        return services;
    }
}
