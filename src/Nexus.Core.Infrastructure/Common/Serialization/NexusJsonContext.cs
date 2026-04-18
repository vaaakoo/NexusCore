using System.Text.Json.Serialization;
using Nexus.Core.Infrastructure.Common.Models;

namespace Nexus.Core.Infrastructure.Common.Serialization;

/// <summary>
/// Source-generated JSON context for high-performance serialization of 
/// Nexus.Core infrastructure models.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Result))]
[JsonSerializable(typeof(Result<Unit>))]
[JsonSerializable(typeof(Result<object>))]
[JsonSerializable(typeof(Result<string>))]
[JsonSerializable(typeof(Result<int>))]
[JsonSerializable(typeof(Result<bool>))]
[JsonSerializable(typeof(Result<IDictionary<string, string[]>>))]
[JsonSerializable(typeof(Result<Dictionary<string, string[]>>))]
internal partial class NexusJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Represents a "nothing" value for generic Result instances where no return is needed.
/// </summary>
public record Unit();
