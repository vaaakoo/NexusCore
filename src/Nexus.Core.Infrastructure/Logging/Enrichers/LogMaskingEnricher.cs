using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Nexus.Core.Infrastructure.Logging.Enrichers;

/// <summary>
/// A Serilog enricher that scans log properties for PII (Personal Identifiable Information).
/// It masks 11-digit numbers and specific property names like 'PersonalId'.
/// Pattern: 01*******89
/// </summary>
public sealed partial class LogMaskingEnricher : ILogEventEnricher
{
    // ── Configuration ────────────────────────────────────────────────────────
    private static readonly string[] SensitivePropertyNames = ["PersonalId", "IdentificationNumber", "Ssn"];
    
    [GeneratedRegex(@"\b\d{11}\b")]
    private static partial Regex IdNumberRegex();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties)
        {
            if (IsSensitiveProperty(property.Key))
            {
                MaskProperty(logEvent, property.Key, property.Value, propertyFactory);
            }
            else if (property.Value is ScalarValue { Value: string stringValue } && IdNumberRegex().IsMatch(stringValue))
            {
                MaskPattern(logEvent, property.Key, stringValue, propertyFactory);
            }
        }
    }

    private static bool IsSensitiveProperty(string name) =>
        SensitivePropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static void MaskProperty(LogEvent logEvent, string name, LogEventPropertyValue value, ILogEventPropertyFactory factory)
    {
        if (value is ScalarValue { Value: string val })
        {
            logEvent.AddOrUpdateProperty(factory.CreateProperty(name, MaskString(val)));
        }
    }

    private void MaskPattern(LogEvent logEvent, string name, string value, ILogEventPropertyFactory factory)
    {
        var masked = IdNumberRegex().Replace(value, m => MaskString(m.Value));
        logEvent.AddOrUpdateProperty(factory.CreateProperty(name, masked));
    }

    private static string MaskString(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 4) return "****";
        
        // Pattern: 01*******89
        return $"{input[..2]}{new string('*', input.Length - 4)}{input[^2..]}";
    }
}
