using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Nexus.Core.Infrastructure.Security;

/// <summary>
/// Provides extension methods for configuring JWT Bearer authentication
/// using settings from <c>appsettings.json</c> under the <c>"Jwt"</c> section.
/// </summary>
public static class JwtExtensions
{
    // ── Configuration section key ──────────────────────────────────────────────
    private const string SectionKey = "Jwt";

    /// <summary>
    /// Registers JWT Bearer authentication and authorisation services,
    /// binding token-validation parameters from <c>IConfiguration["Jwt:*"]</c>.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at startup if the <c>"Jwt"</c> configuration section is missing or
    /// the <c>"Jwt:SecretKey"</c> value is absent or too short (≥ 32 chars required).
    /// </exception>
    public static IServiceCollection AddNexusSecurity(
        this IServiceCollection services,
        IConfiguration          config)
    {
        var jwtSettings = config.GetSection(SectionKey).Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                $"Missing required configuration section \"{SectionKey}\". "
              + "Ensure appsettings.json contains a \"Jwt\" block with SecretKey, Issuer, and Audience.");

        jwtSettings.Validate();

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

        services
            .AddSingleton(jwtSettings)              // expose settings for token generation helpers
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken                 = false; // don't store token in AuthenticationProperties
                options.RequireHttpsMetadata      = !IsRunningInDevelopment();
                options.TokenValidationParameters = BuildValidationParameters(jwtSettings, signingKey);

                // ── Event hooks for rich structured logging ────────────────
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();

                        logger.LogWarning(
                            ctx.Exception,
                            "JWT authentication failed | TraceId={TraceId} | Reason={Reason}",
                            ctx.HttpContext.TraceIdentifier,
                            ctx.Exception.GetType().Name);

                        return Task.CompletedTask;
                    },

                    OnTokenValidated = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();

                        var subject = ctx.Principal?.FindFirst("sub")?.Value ?? "unknown";

                        logger.LogDebug(
                            "JWT validated | TraceId={TraceId} | Subject={Subject}",
                            ctx.HttpContext.TraceIdentifier,
                            subject);

                        return Task.CompletedTask;
                    },

                    OnChallenge = ctx =>
                    {
                        // Suppress the default WWW-Authenticate redirect behaviour for API clients.
                        ctx.HandleResponse();
                        ctx.Response.StatusCode  = 401;
                        ctx.Response.ContentType = "application/json";
                        return ctx.Response.WriteAsync(
                            """{"isSuccess":false,"statusCode":401,"errorMessage":"Unauthorized. A valid JWT Bearer token is required."}""");
                    },

                    OnForbidden = ctx =>
                    {
                        ctx.Response.StatusCode  = 403;
                        ctx.Response.ContentType = "application/json";
                        return ctx.Response.WriteAsync(
                            """{"isSuccess":false,"statusCode":403,"errorMessage":"Forbidden. You do not have permission to access this resource."}""");
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static TokenValidationParameters BuildValidationParameters(
        JwtSettings       settings,
        SymmetricSecurityKey signingKey) =>
        new()
        {
            ValidateIssuer           = true,
            ValidIssuer              = settings.Issuer,

            ValidateAudience         = true,
            ValidAudience            = settings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = signingKey,

            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromSeconds(settings.ClockSkewSeconds),

            RequireExpirationTime    = true,
            RequireSignedTokens      = true,

            // Use the standard "sub" claim as the NameIdentifier.
            NameClaimType            = "sub",
            RoleClaimType            = "roles"
        };

    /// <summary>
    /// Lightweight check for development mode without requiring IHostEnvironment
    /// to be injected into a static extension method.
    /// </summary>
    private static bool IsRunningInDevelopment() =>
        string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);
}

// ── Configuration model ────────────────────────────────────────────────────────

/// <summary>
/// Strongly-typed representation of the <c>"Jwt"</c> configuration section.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>The HMAC-SHA256 signing secret. Must be at least 32 characters.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Expected <c>iss</c> claim value.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Expected <c>aud</c> claim value.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Token lifetime in minutes. Defaults to <c>60</c>.</summary>
    public int ExpiryMinutes { get; init; } = 60;

    /// <summary>
    /// Allowed clock skew in seconds to tolerate minor time-sync differences
    /// between token issuer and validator. Defaults to <c>30</c> seconds.
    /// </summary>
    public int ClockSkewSeconds { get; init; } = 30;

    /// <summary>Validates required fields at startup to give early, actionable errors.</summary>
    internal void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SecretKey) || SecretKey.Length < 32)
            errors.Add("Jwt:SecretKey must be at least 32 characters long.");

        if (string.IsNullOrWhiteSpace(Issuer))
            errors.Add("Jwt:Issuer is required.");

        if (string.IsNullOrWhiteSpace(Audience))
            errors.Add("Jwt:Audience is required.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"JWT configuration is invalid:{Environment.NewLine}"
              + string.Join(Environment.NewLine, errors.Select(e => $"  • {e}")));
    }
}
