using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Reflection;
using Nexus.Core.Infrastructure.Logging.Attributes;

namespace Nexus.Core.Infrastructure.Logging.Json;

/// <summary>
/// A modern System.Text.Json policy using TypeInfo modifiers to automatically 
/// mask properties decorated with the [SensitiveData] attribute.
/// This approach is more performant and robust than custom converters for large object graphs.
/// </summary>
public static class NexusMaskingPolicy
{
    /// <summary>
    /// A modifier for <see cref="DefaultJsonTypeInfoResolver"/> that identifies
    /// sensitive properties and replaces their serialization logic with a masking function.
    /// </summary>
    public static void MaskSensitiveProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties)
        {
            var attribute = property.AttributeProvider?.GetCustomAttributes(typeof(SensitiveDataAttribute), true);
            
            if (attribute is not null && attribute.Length > 0)
            {
                var originalGetter = property.Get;
                if (originalGetter is null) continue;

                // Replace the getter with a masking version if the value is a string
                property.Get = obj =>
                {
                    var value = originalGetter(obj);
                    if (value is string s)
                    {
                        return MaskString(s);
                    }
                    return value;
                };
            }
        }
    }

    private static string MaskString(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 4) return "****";
        
        // Pattern: 01*******89
        return $"{input[..2]}{new string('*', input.Length - 4)}{input[^2..]}";
    }
}
