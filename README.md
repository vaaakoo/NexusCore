# Nexus.Core — Senior-Level .NET 10 Infrastructure Framework

![Build Status](https://github.com/vaaakoo/NexusCore/actions/workflows/ci.yml/badge.svg)

```csharp
// Program.cs — that's literally it.
builder.AddNexusInfrastructure();
```

---

## 🚀 Overview

**Nexus.Core** is a production-ready infrastructure framework designed for .NET 10 Web APIs. It eliminates boilerplate by providing pre-wired, senior-level cross-cutting concerns with zero manual configuration required.

### Key Pillars
- **🏗️ Structured Logging**: Serilog configured with automatic PII masking (01*******89), daily-rolling file sinks, and thread/trace enrichment.
- **🛡️ Data Masking**: Native attribute-based masking (`[SensitiveData]`) for JSON responses and regex-based masking for log streams.
- **🔒 JWT Security**: Robust authentication/authorization with custom JSON 401/403 responses and secure-by-default metadata.
- **⚡ Functional Errors**: A generic `Result<T>` success/failure monad for Railway Oriented Programming.
- **🚑 Global Safety Net**: Middleware that catches all unhandled exceptions, logs them with `TraceId`, and returns standardised JSON contracts.

---

## 🛠️ Quick Start

### 1. Configure the basics
Add your settings to `appsettings.json`:
```json
{
  "Jwt": {
    "SecretKey": "<secure-32-char-key>",
    "Issuer": "nexus-api",
    "Audience": "nexus-clients"
  }
}
```

### 2. Activate the Infrastructure
```csharp
var builder = WebApplication.CreateBuilder(args);

// ① One line to wire Logging, JWT, Validation, and OpenAPI
builder.AddNexusInfrastructure();

var app = builder.Build();

// ② One line to activate the middleware pipeline
app.UseNexusInfrastructure();

app.Run();
```

---

## 💎 Advanced Features

### PII Protection (Masking)
Decorate properties with `[SensitiveData]` to ensure they are masked in API responses.
```csharp
public record User(string Name, [property: SensitiveData] string PersonalId);
```
*Logs will also automatically mask 11-digit numbers and field names like 'PersonalId'.*

### Functional Chaining
```csharp
public Result<User> GetUser(Guid id) =>
    _db.Find(id).ToResult()
       .Map(u => EnrichData(u))
       .Bind(u => ValidateStatus(u));
```

---

## 🧪 Testing & CI

The framework includes a comprehensive test suite (xUnit + FluentAssertions + NSubstitute) covering:
- `Result<T>` functional logic (100% coverage)
- Global Exception Middleware & TraceId logging
- Automatic Validator Filters
- JWT Service Registration

**Run tests locally:**
```bash
dotnet test Nexus.Core.slnx
```

---

## 🏛️ Architecture Decisions
- **.NET 10 & C# 14**: Utilising Primary Constructors and File-scoped namespaces.
- **Clean Architecture**: Strong decoupling of infrastructure from business logic.
- **FrameworkReference**: Using `Microsoft.AspNetCore.App` in ClassLibs to stay lean.

---

## 📜 License
MIT — *Built with zero tolerance for boilerplate.*
