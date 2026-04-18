# Nexus.Core — Production-Grade .NET 10 Infrastructure Framework

![Build Status](https://github.com/vaoakoo/NexusCore/actions/workflows/ci-cd.yml/badge.svg)
![License](https://img.shields.io/github/license/vaoakoo/NexusCore)
![.NET Version](https://img.shields.io/badge/.NET-10-blue)

> **"The Antigravity Effect"** — collapse an entire enterprise infrastructure stack down to a single, expressive line of code.

```csharp
// Program.cs — that's literally it.
builder.AddNexusInfrastructure();
```

---

## Overview

**Nexus.Core** is a senior-level, reusable infrastructure framework for .NET 10 Web APIs. It implements Clean Architecture principles and ships every cross-cutting concern you need in production — structured logging, JWT authentication, global error handling, and a functional `Result<T>` monad — pre-wired, battle-tested, and ready to drop into any ASP.NET Core application.

| Concern | Technology | Location |
|---|---|---|
| Structured Logging | Serilog + Enrichers | `Logging/LoggingExtensions.cs` |
| JWT Authentication | Microsoft.AspNetCore.Authentication.JwtBearer | `Security/JwtExtensions.cs` |
| Global Error Handling | Custom ASP.NET Core Middleware | `Middleware/GlobalExceptionMiddleware.cs` |
| Functional Error Model | Railway-Oriented `Result<T>` | `Common/Models/Result.cs` |
| Resilience Pipelines | Polly 8 (wired-in, extend as needed) | — |
| Validation | FluentValidation (wired-in, extend as needed) | — |

---

## Solution Structure

```
Nexus.Core/
├── src/
│   └── Nexus.Core.Infrastructure/          # ← The reusable NuGet-ready library
│       ├── Common/
│       │   └── Models/
│       │       └── Result.cs               # Generic Result<T> monad
│       ├── Extensions/
│       │   └── ServiceCollectionExtensions.cs  # Master composition root
│       ├── Logging/
│       │   └── LoggingExtensions.cs        # Serilog configuration
│       ├── Middleware/
│       │   └── GlobalExceptionMiddleware.cs # Catch-all error handler
│       ├── Security/
│       │   └── JwtExtensions.cs            # JWT Bearer + JwtSettings
│       └── Validation/                     # (extend with FluentValidation behaviours)
│
├── samples/
│   └── Nexus.Sample.Api/                   # ← Demonstration Web API
│       ├── Program.cs                      # "The Antigravity Effect" demo
│       └── appsettings.json
│
└── Nexus.Core.slnx
```

---

## Quick Start

### 1. Configure `appsettings.json`

```json
{
  "Jwt": {
    "SecretKey":        "<your-secret-≥32-chars>",
    "Issuer":           "your-api-name",
    "Audience":         "your-clients",
    "ExpiryMinutes":    60,
    "ClockSkewSeconds": 30
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

> **⚠️ Security Notice** — Never commit real `SecretKey` values. Use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), Azure Key Vault, or environment-variable overrides in production.

### 2. Wire the infrastructure

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.AddNexusInfrastructure();   // ← The Antigravity Effect™

var app = builder.Build();

app.UseNexusInfrastructure();       // ← Middleware pipeline (also one line)

app.MapGet("/health", () => Result<string>.Success("OK")).AllowAnonymous();

app.Run();
```

That's it. Every concern below is active automatically.

---

## Feature Deep-Dive

### `Result<T>` — Functional Error Handling

A discriminated-union result type inspired by Railway Oriented Programming. Use it instead of throwing exceptions for expected business failures.

```csharp
// In a service / use-case
public Result<User> GetUser(Guid id)
{
    var user = _repo.Find(id);
    return user is null
        ? Result.NotFound<User>($"User {id} not found.")
        : Result.Success(user);
}

// In a minimal-API endpoint
app.MapGet("/users/{id}", (Guid id, UserService svc) =>
{
    var result = svc.GetUser(id);
    return result.IsSuccess
        ? Results.Ok(result)
        : Results.Json(result, statusCode: result.StatusCode);
});
```

**Fluent projection helpers:**

```csharp
var result = GetUser(id)
    .Map(u => new UserDto(u.Id, u.Name))   // transform on success
    .Bind(dto => ValidateDto(dto));        // chain another Result-producing op
```

---

### `GlobalExceptionMiddleware` — Zero-Boilerplate Error Handling

All unhandled exceptions are caught, **logged** with full context (TraceId, HTTP method, path, exception type), and returned as a standardised JSON response. Endpoint code stays clean — no try/catch blocks needed.

```json
// HTTP 500 response body
{
  "isSuccess": false,
  "statusCode": 500,
  "errorMessage": "An unexpected error occurred. Please try again later or contact support.",
  "traceId": "0HN4S6JLQB0BQ:00000001",
  "timestamp": "2026-04-19T00:10:00.000Z"
}
```

**Exception → Status Code mapping (extensible):**

| Exception Type | HTTP Status |
|---|---|
| `ArgumentException` / `InvalidOperationException` | 400 Bad Request |
| `UnauthorizedAccessException` | 401 Unauthorized |
| `KeyNotFoundException` | 404 Not Found |
| `NotImplementedException` | 501 Not Implemented |
| `OperationCanceledException` | 499 Client Closed |
| Everything else | 500 Internal Server Error |

> **Security Design** — 5xx error messages are **never** returned verbatim to the client; a generic message is substituted to prevent information leakage. 4xx messages (which are safe to expose) are passed through.

---

### `LoggingExtensions` — Structured Logging with Serilog

Every log event is enriched with:

| Enricher | Value |
|---|---|
| `MachineName` | Physical/container hostname |
| `Environment` | `ASPNETCORE_ENVIRONMENT` value |
| `Application` | Assembly name |
| `TraceId` / `SpanId` | W3C distributed tracing |
| `ThreadId` | Useful for concurrency diagnostics |
| `ExceptionDetail` | Full destructured exception graph |

Console output format:
```
[12:34:56 INF] Nexus.Sample.Api | TraceId=0HN4... | HTTP GET /me responded 200 in 4.2ms
```

Additional sinks (Seq, Elastic, Application Insights) are configured via `appsettings.json` under the `"Serilog"` key — the library stays sink-agnostic.

---

### `JwtExtensions` — JWT Bearer Authentication

- Validates **Issuer**, **Audience**, **Lifetime**, and **Signature** on every request.
- Configurable **clock skew** tolerance for distributed systems.
- Structured log event on every failed authentication (with TraceId).
- Custom **401/403** JSON responses that match the `Result` contract — no HTML error pages.
- `HTTPS required` automatically toggled off in Development.

---

### Middleware Pipeline Order (`UseNexusInfrastructure`)

```
Inbound request
    ↓
① GlobalExceptionMiddleware    ← catch everything below
    ↓
② SerilogRequestLogging        ← structured HTTP access log
    ↓
③ UseHttpsRedirection          ← HTTPS enforcement
    ↓
④ UseAuthentication            ← JWT validation
    ↓
⑤ UseAuthorization             ← policy enforcement
    ↓
Your endpoints
```

---

## Sample API Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | Public | Health probe, returns `Result<object>` |
| `GET` | `/echo/{message}` | Public | Echoes message, demonstrates `Result<T>` |
| `GET` | `/me` | 🔒 JWT | Returns authenticated user's claims |
| `GET` | `/forecast` | 🔒 JWT | Sample business endpoint with `Result<T[]>` |
| `GET` | `/demo/throw` | 🔒 JWT | Forces exception to demo `GlobalExceptionMiddleware` |

---

## Architecture Decisions

| Decision | Rationale |
|---|---|
| **File-scoped namespaces** | Reduces nesting noise, C# 10+ best practice |
| **Primary Constructors** | C# 12+ — concise DI injection with less ceremony |
| **`sealed` classes** | Explicit intent; prevents accidental inheritance of infrastructure types |
| **Internal `ErrorResponse` record** | Keeps the internal wire format isolated from the public `Result<T>` API |
| **`FrameworkReference: Microsoft.AspNetCore.App`** | Allows the ClassLib to consume all ASP.NET Core types without duplicating package refs |
| **Startup validation in `JwtSettings.Validate()`** | Fail fast at startup with actionable messages rather than obscure runtime errors |
| **Sink-agnostic logging** | Library doesn't force a log sink; consumers add Seq/Elastic/etc. via config |

---

## Extending Nexus.Core

### Add a Polly resilience pipeline

```csharp
// In AddNexusInfrastructure or your own extension
services.AddResiliencePipeline("default", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions())
        .AddTimeout(TimeSpan.FromSeconds(10));
});
```

### Add FluentValidation

```csharp
services.AddValidatorsFromAssemblyContaining<MyValidator>();
```

### Add a custom `Result` extension

```csharp
public static IResult ToHttpResult<T>(this Result<T> result) =>
    result.IsSuccess
        ? Results.Ok(result)
        : Results.Json(result, statusCode: result.StatusCode);
```

---

## Requirements

- **.NET 10 SDK** (Preview 4+)
- No external infrastructure dependencies at runtime (Serilog, Polly, FluentValidation are all embedded)

---

## License

MIT — use freely in commercial and open-source projects.

---

*Built with Clean Architecture principles, C# 14 language features, and zero tolerance for boilerplate.*
