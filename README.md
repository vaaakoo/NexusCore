# Nexus.Core Infrastructure Framework

Nexus.Core is a high-performance, production-grade infrastructure library for .NET 10 Web APIs. It provides a standardized foundation for enterprise applications, centralizing cross-cutting concerns such as logging, security, and error handling into a modular and discoverable API.

---

## Core Capabilities

- **Structured Logging**: Pre-configured Serilog integration with daily-rolling file sinks and PII masking.
- **PII Protection**: Integrated data masking for both log streams and JSON API responses using the `[SensitiveData]` attribute and `LogMaskingEnricher`.
- **Standardized Security**: Robust JWT Bearer authentication and authorization with custom rejection handlers.
- **Functional Error Handling**: Implementation of the `Result<T>` pattern to facilitate Railway Oriented Programming and consistent API contracts.
- **Global Exception Management**: Centralized middleware to intercept unhandled exceptions, ensuring secure error reporting and automated diagnostic logging (TraceId).

---

## Installation

The package is available via the GitHub Packages Registry.

### 1. Configure Package Source
Add the following to your `nuget.config`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="NexusCore" value="https://nuget.pkg.github.com/vaoakoo/index.json" />
  </packageSources>
</configuration>
```

### 2. Add Package
```bash
dotnet add package Nexus.Core.Infrastructure
```

---

## Getting Started

### Configuration
Configure your security and logging settings in `appsettings.json`:
```json
{
  "Jwt": {
    "SecretKey": "YOUR_SECURE_32_CHARACTER_KEY",
    "Issuer": "nexus-api",
    "Audience": "nexus-clients"
  }
}
```

### Initialization
Register and activate the infrastructure in your `Program.cs`:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Infrastructure (Logging, Security, Validation, OpenAPI)
builder.AddNexusInfrastructure();

var app = builder.Build();

// Activate Middleware Pipeline
app.UseNexusInfrastructure();

app.Run();
```

---

## Features

### PII Masking
Properties marked with `[SensitiveData]` are automatically masked in JSON responses.
```csharp
public record UserInfo(string Name, [property: SensitiveData] string PersonalId);
```

### Functional Chaining
Utilize the `Result<T>` type to chain business operations without deeply nested try-catch blocks.
```csharp
public Result<User> ProcessUser(Guid id) =>
    _repository.Get(id)
        .Map(user => ApplyBusinessRules(user))
        .Bind(user => SaveChanges(user));
```

---

## Technical Standards
- **Target Framework**: .NET 10
- **Language Level**: C# 14
- **Architecture**: Clean Architecture / Ports & Adapters
- **Quality Assurance**: 100% coverage of core functional logic via xUnit and NSubstitute.

---

## License
Licensed under the MIT License.
