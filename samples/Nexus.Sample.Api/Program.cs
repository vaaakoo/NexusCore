using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Nexus.Core.Infrastructure.Common.Models;
using Nexus.Core.Infrastructure.Extensions;
using Nexus.Core.Infrastructure.Logging.Attributes;
using Nexus.Core.Infrastructure.Security;
using Nexus.Core.Infrastructure.Validation;

// ┌─────────────────────────────────────────────────────────────────────────────┐
// │  Nexus.Sample.Api – Program.cs                                             │
// │                                                                             │
// │  The Antigravity Effect™: ONE line activates the entire infrastructure.    │
// └─────────────────────────────────────────────────────────────────────────────┘

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════════
// ① THE ANTIGRAVITY EFFECT – activate the entire Nexus infrastructure stack
//    with a single, expressive call.
// ═══════════════════════════════════════════════════════════════════════════════
builder.AddNexusInfrastructure();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════════════
// ② Configure the middleware pipeline (also a single Nexus call).
// ═══════════════════════════════════════════════════════════════════════════════
app.UseNexusInfrastructure();

// ── OpenAPI (development only) ────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// ══════════════════════════════════════════════════════════════════════════════
// ⚠️  DEV-ONLY: Token issuance endpoint — remove in production!
//     Use your real Auth server (Keycloak, Auth0, custom STS) instead.
// ══════════════════════════════════════════════════════════════════════════════

app.MapPost("/token", (TokenRequest req, JwtSettings jwtSettings) =>
{
    var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey));
    var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var now     = DateTime.UtcNow;
    var expires = now.AddMinutes(jwtSettings.ExpiryMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub,    req.Username),
        new(JwtRegisteredClaimNames.Name,   req.Username),
        new(JwtRegisteredClaimNames.Jti,    Guid.NewGuid().ToString()),
        new(JwtRegisteredClaimNames.Iat,    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        new("roles",                        req.Role ?? "user")
    };

    var token = new JwtSecurityToken(
        issuer:             jwtSettings.Issuer,
        audience:           jwtSettings.Audience,
        claims:             claims,
        notBefore:          now,
        expires:            expires,
        signingCredentials: creds);

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(Result<object>.Success(new
    {
        AccessToken = tokenString,
        TokenType   = "Bearer",
        ExpiresIn   = jwtSettings.ExpiryMinutes * 60,
        ExpiresAt   = expires,
        IssuedFor   = req.Username
    }));
})
.WithName("IssueTestToken")
.WithSummary("⚠️ DEV-ONLY – Issues a signed JWT for testing protected endpoints.")
.WithValidation<TokenRequest>() // ← Senior Addition: Auto-validation
.AllowAnonymous();

app.MapGet("/demo/masking", (ILogger<Program> logger) =>
{
    var pii = "12345678901"; // 11 digits
    logger.LogInformation("Processing user with PersonalId: {PersonalId}", pii);
    
    return Results.Ok(Result<UserInfo>.Success(new UserInfo("John Doe", pii)));
})
.WithName("MaskingDemo")
.WithSummary("Demonstrates Log and JSON masking. Check console/file logs and JSON response.");

// ── Protected: Validation Demo ───────────────────────────────────────────────

app.MapPost("/demo/validate", [Authorize] (SampleData data) =>
    Results.Ok(Result<SampleData>.Success(data)))
    .WithName("ValidationDemo")
    .WithSummary("Demonstrates automatic FluentValidation integration. Requires JWT.")
    .WithValidation<SampleData>(); // ← Senior Addition: Auto-validation

// ══════════════════════════════════════════════════════════════════════════════
// Sample endpoints that showcase JWT + GlobalExceptionMiddleware + Result<T>
// ══════════════════════════════════════════════════════════════════════════════

// ── Public: health check ─────────────────────────────────────────────────────

app.MapGet("/health", () =>
    Result<object>.Success(new
    {
        Status    = "Healthy",
        Service   = "Nexus.Sample.Api",
        Timestamp = DateTimeOffset.UtcNow,
        Machine   = Environment.MachineName
    }))
    .WithName("HealthCheck")
    .WithSummary("Returns the health status of the API.")
    .AllowAnonymous();

// ── Public: echo – demonstrates Result<T> on a non-protected route ───────────

app.MapGet("/echo/{message}", (string message) =>
    string.IsNullOrWhiteSpace(message)
        ? Results.Json(Result.BadRequest("Message cannot be empty."), statusCode: 400)
        : Results.Ok(Result<string>.Success(message)))
    .WithName("Echo")
    .WithSummary("Echoes back the provided message, demonstrating Result<T>.")
    .AllowAnonymous();

// ── Protected: current user info – requires a valid JWT ──────────────────────

app.MapGet("/me", [Authorize] (HttpContext ctx) =>
{
    var identity = ctx.User.Identity;

    if (identity is not { IsAuthenticated: true })
        return Results.Json(Result.Unauthorized(), statusCode: 401);

    var claims = ctx.User.Claims
        .Select(c => new { c.Type, c.Value })
        .ToList();

    return Results.Ok(Result<object>.Success(new
    {
        IsAuthenticated = true,
        Name            = identity.Name,
        Claims          = claims
    }));
})
.WithName("GetCurrentUser")
.WithSummary("Returns the authenticated user's identity and claims. Requires JWT.");

// ── Protected: force an exception – demonstrates GlobalExceptionMiddleware ────

app.MapGet("/demo/throw", [Authorize] () =>
{
    // This will be caught by GlobalExceptionMiddleware and returned as a
    // standardised Result JSON response – no try/catch needed in endpoint code.
    throw new InvalidOperationException(
        "This is a demo exception to showcase the GlobalExceptionMiddleware.");
})
.WithName("DemoThrow")
.WithSummary("Intentionally throws to demonstrate the global error handler. Requires JWT.");

// ── Protected: a simple business-logic demonstration ─────────────────────────

app.MapGet("/forecast", [Authorize] () =>
{
    var summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    var forecasts = Enumerable.Range(1, 5).Select(i => new WeatherForecast(
        Date:        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i)),
        TempCelsius: Random.Shared.Next(-20, 55),
        Summary:     summaries[Random.Shared.Next(summaries.Length)]
    )).ToArray();

    return Results.Ok(Result<WeatherForecast[]>.Success(forecasts));
})
.WithName("GetWeatherForecast")
.WithSummary("Returns a 5-day forecast. Requires JWT.");

app.Run();

// ── Local records ─────────────────────────────────────────────────────────────

record WeatherForecast(DateOnly Date, int TempCelsius, string? Summary)
{
    public int TempFahrenheit => 32 + (int)(TempCelsius / 0.5556);
}

public record UserInfo(string Name, [property: SensitiveData] string PersonalId);

/// <summary>Request body for the DEV-ONLY /token endpoint.</summary>
public record TokenRequest(string Username, string? Role = "user");

/// <summary>Sample data for validation demo.</summary>
public record SampleData(string Email, int Age, decimal Amount);

// ── Validators ────────────────────────────────────────────────────────────────

public sealed class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().EmailAddress().WithMessage("A valid email is required as username.");
    }
}

public sealed class SampleDataValidator : AbstractValidator<SampleData>
{
    public SampleDataValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).InclusiveBetween(18, 99).WithMessage("Age must be between 18 and 99.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
    }
}
