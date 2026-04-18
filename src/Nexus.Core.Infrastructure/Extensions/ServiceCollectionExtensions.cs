using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexus.Core.Infrastructure.Logging;
using Nexus.Core.Infrastructure.Logging.Json;
using Nexus.Core.Infrastructure.Middleware;
using Nexus.Core.Infrastructure.Security;
using Nexus.Core.Infrastructure.Resilience;
using Nexus.Core.Infrastructure.Observability;
using Nexus.Core.Infrastructure.Common.Serialization;
using Serilog;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Nexus.Core.Infrastructure.Extensions;

/// <summary>
/// Top-level composition root for the Nexus infrastructure layer.
/// Exposes a single, discoverable entry-point that assembles all cross-cutting
/// concerns (logging, security, HTTP context) so that consuming applications need
/// only one line to opt into the full stack.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the complete Nexus infrastructure stack into the DI container.
    /// </summary>
    /// <remarks>
    /// What gets registered:
    /// <list type="bullet">
    ///   <item><b>Logging</b> – Serilog with structured enrichers (TraceId, MachineName, Environment).</item>
    ///   <item><b>Security</b> – JWT Bearer authentication + authorisation policies.</item>
    ///   <item><b>HttpContextAccessor</b> – <see cref="IHttpContextAccessor"/> for services that need
    ///     access to the current request context outside of middleware.</item>
    ///   <item><b>OpenAPI</b> – Minimal API metadata endpoint (Scalar / Swagger-compatible).</item>
    /// </list>
    /// </remarks>
    /// <param name="services">The application's <see cref="IServiceCollection"/>.</param>
    /// <param name="config">The application's merged <see cref="IConfiguration"/>.</param>
    /// <param name="environment">The hosting environment; used to resolve environment-specific logging rules.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for fluent chaining.</returns>
    public static IServiceCollection AddNexusInfrastructure(
        this IServiceCollection services,
        IConfiguration          config,
        IHostEnvironment        environment)
    {
        services
            .AddNexusLogging(config, environment)   // → Serilog
            .AddNexusSecurity(config)               // → JWT Bearer + Authorization
            .AddNexusObservability(environment)     // → OpenTelemetry
            .AddNexusResilience()                   // → Polly v8 Pipelines
            .AddHttpContextAccessor()               // → IHttpContextAccessor
            .AddOpenApi();                          // → OpenAPI / Scalar metadata

        // ── Senior Addition: JSON Masking ────────────────────────────────────
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                new NexusJsonContext(),
                new DefaultJsonTypeInfoResolver { Modifiers = { NexusMaskingPolicy.MaskSensitiveProperties } }
            );
        });

        // ── Senior Addition: Validation & Resilience ─────────────────────────
        services.AddValidatorsFromAssemblies(new[] { 
            typeof(ServiceCollectionExtensions).Assembly, 
            Assembly.GetCallingAssembly() 
        });

        return services;
    }

    /// <summary>
    /// Overload that accepts a <see cref="WebApplicationBuilder"/> so that the
    /// Serilog host integration (UseSerilog) is applied before the host is built,
    /// ensuring the very first log events are captured by Serilog.
    /// </summary>
    /// <remarks>
    /// Prefer this overload in <c>Program.cs</c> over the
    /// <see cref="AddNexusInfrastructure(IServiceCollection, IConfiguration, IHostEnvironment)"/>
    /// overload to guarantee no log events are lost during startup.
    /// </remarks>
    public static WebApplicationBuilder AddNexusInfrastructure(
        this WebApplicationBuilder builder)
    {
        builder
            .AddNexusLogging()                              // host-level Serilog wiring
            .Services
            .AddNexusSecurity(builder.Configuration)       // JWT Bearer + Authorization
            .AddNexusObservability(builder.Environment)    // OpenTelemetry
            .AddNexusResilience()                          // Polly v8 Pipelines
            .AddHttpContextAccessor()                       // IHttpContextAccessor
            .AddOpenApi();                                  // OpenAPI / Scalar metadata

        // ── Senior Addition: JSON Masking ────────────────────────────────────
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                new NexusJsonContext(),
                new DefaultJsonTypeInfoResolver { Modifiers = { NexusMaskingPolicy.MaskSensitiveProperties } }
            );
        });
        // ── Senior Addition: Validation ──────────────────────────────────────
        builder.Services.AddValidatorsFromAssemblies(new[] { 
            typeof(ServiceCollectionExtensions).Assembly, 
            Assembly.GetCallingAssembly() 
        });

        return builder;
    }

    /// <summary>
    /// Configures the Nexus middleware pipeline in the correct order.
    /// Call this after <c>builder.Build()</c>.
    /// </summary>
    /// <remarks>
    /// Pipeline order:
    /// <list type="number">
    ///   <item>Global exception handler (outermost – catches everything below).</item>
    ///   <item>Serilog request logging.</item>
    ///   <item>HTTPS redirection.</item>
    ///   <item>Authentication.</item>
    ///   <item>Authorisation.</item>
    /// </list>
    /// </remarks>
    public static WebApplication UseNexusInfrastructure(this WebApplication app)
    {
        app.UseNexusExceptionHandler();     // ① global safety net
        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        });                                 // ② structured request log
        app.UseHttpsRedirection();          // ③ HTTPS redirect
        app.UseAuthentication();            // ④ validate JWT
        app.UseAuthorization();             // ⑤ enforce policies

        return app;
    }
}
