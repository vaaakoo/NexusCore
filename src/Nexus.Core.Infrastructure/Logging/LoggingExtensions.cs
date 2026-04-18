using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexus.Core.Infrastructure.Logging.Enrichers;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using Serilog.Exceptions;

namespace Nexus.Core.Infrastructure.Logging;

/// <summary>
/// Provides extension methods for configuring Serilog as the application's
/// structured logging provider via <see cref="IServiceCollection"/> and
/// <see cref="WebApplicationBuilder"/>.
/// </summary>
public static class LoggingExtensions
{
    // ── Configuration key used to override the global minimum level ────────────
    private const string MinimumLevelKey = "Serilog:MinimumLevel";

    /// <summary>
    /// Replaces the default Microsoft logging pipeline with Serilog and wires up
    /// a standardised set of enrichers and sinks appropriate for production use.
    /// </summary>
    /// <remarks>
    /// Enrichers configured:
    /// <list type="bullet">
    ///   <item><c>MachineName</c> – physical or container hostname.</item>
    ///   <item><c>Environment</c> – value of <c>ASPNETCORE_ENVIRONMENT</c>.</item>
    ///   <item><c>Application</c> – entry-assembly name.</item>
    ///   <item><c>TraceId</c> / <c>SpanId</c> – W3C distributed tracing identifiers.</item>
    ///   <item><c>ThreadId</c> – useful for diagnosing concurrency issues.</item>
    ///   <item><c>ExceptionDetail</c> – full destructured exception graph (Serilog.Exceptions).</item>
    /// </list>
    /// </remarks>
    public static WebApplicationBuilder AddNexusLogging(this WebApplicationBuilder builder)
    {
        var environment = builder.Environment.EnvironmentName;
        var appName     = builder.Environment.ApplicationName;
        var minLevel    = builder.Configuration.GetValue(MinimumLevelKey, LogEventLevel.Information);

        builder.Host.UseSerilog((ctx, services, cfg) =>
            cfg.ConfigureNexusSerilog(ctx.Configuration, environment, appName, minLevel));

        return builder;
    }

    /// <summary>
    /// Registers Serilog services without taking over the host builder – useful when you
    /// want to compose logging inside <see cref="IServiceCollection"/> extensions.
    /// </summary>
    public static IServiceCollection AddNexusLogging(
        this IServiceCollection services,
        IConfiguration          configuration,
        IHostEnvironment        environment)
    {
        var minLevel = configuration.GetValue(MinimumLevelKey, LogEventLevel.Information);
        var logger   = new LoggerConfiguration()
            .ConfigureNexusSerilog(configuration, environment.EnvironmentName, environment.ApplicationName, minLevel)
            .CreateLogger();

        Log.Logger = logger;
        services.AddSerilog(logger);
        return services;
    }

    // ── Core configuration builder (shared by both overloads) ─────────────────

    private static LoggerConfiguration ConfigureNexusSerilog(
        this LoggerConfiguration cfg,
        IConfiguration           configuration,
        string                   environment,
        string                   appName,
        LogEventLevel            minimumLevel)
    {
        cfg
            // ── Minimum levels ───────────────────────────────────────────────
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft",                         LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore",              LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore",     LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient",        LogEventLevel.Warning)

            // ── Enrichers ────────────────────────────────────────────────────
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", appName)
            .Enrich.WithProperty("Environment", environment)
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .Enrich.With<LogMaskingEnricher>()

            // ── Sinks ────────────────────────────────────────────────────────
            
            // Console: Structured readable text
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] "
                              + "{SourceContext} | TraceId={TraceId} "
                              + "| {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)

            // File: Beautified JSON for machine processing and audit
            .WriteTo.File(
                formatter: new JsonFormatter(renderMessage: true, closingDelimiter: Environment.NewLine),
                path:      "Logs/nexus-log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)

            // Read any additional sink configuration (e.g. Seq, File) from
            // appsettings.json under the "Serilog" key so that the infrastructure
            // library remains sink-agnostic.
            .ReadFrom.Configuration(configuration);

        return cfg;
    }
}
