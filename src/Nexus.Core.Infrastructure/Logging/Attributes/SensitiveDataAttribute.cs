namespace Nexus.Core.Infrastructure.Logging.Attributes;

/// <summary>
/// Marks a property as containing sensitive data (e.g., PersonalId, Password).
/// When used with <see cref="Nexus.Core.Infrastructure.Logging.Json.NexusMaskingPolicy"/>,
/// the value will be masked in JSON API responses.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute;
