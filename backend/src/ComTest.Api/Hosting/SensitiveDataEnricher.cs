using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace ComTest.Api.Hosting;

/// <summary>
/// Last line of defence for logs: masks values by property name and by shape, so that a
/// token, password or personal detail logged by accident does not reach log storage.
/// </summary>
public sealed partial class SensitiveDataEnricher : ILogEventEnricher
{
    private const string Mask = "***";

    private static readonly string[] SensitiveNameParts =
    [
        "password", "passwd", "pwd", "secret", "token", "authorization", "cookie", "apikey", "api_key",
        "otp", "recoverycode", "privatekey", "email", "phone", "connectionstring",
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            var masked = IsSensitiveName(name) ? new ScalarValue(Mask) : MaskValue(value);

            if (!ReferenceEquals(masked, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, masked));
            }
        }
    }

    internal static string MaskText(string text)
    {
        var result = JwtPattern().Replace(text, Mask);
        result = BearerPattern().Replace(result, "Bearer " + Mask);
        result = ConnectionSecretPattern().Replace(result, "$1=" + Mask);
        result = EmailPattern().Replace(result, Mask + "@$1");
        result = PhonePattern().Replace(result, Mask);

        return result;
    }

    private static bool IsSensitiveName(string name) =>
        SensitiveNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static LogEventPropertyValue MaskValue(LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                var masked = MaskText(text);
                return string.Equals(masked, text, StringComparison.Ordinal) ? value : new ScalarValue(masked);

            case StructureValue structure:
                return new StructureValue(
                    structure.Properties.Select(property => new LogEventProperty(
                        property.Name,
                        IsSensitiveName(property.Name) ? new ScalarValue(Mask) : MaskValue(property.Value))),
                    structure.TypeTag);

            case SequenceValue sequence:
                return new SequenceValue(sequence.Elements.Select(MaskValue));

            case DictionaryValue dictionary:
                return new DictionaryValue(dictionary.Elements.Select(element =>
                    new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        element.Key,
                        element.Key.Value is string key && IsSensitiveName(key)
                            ? new ScalarValue(Mask)
                            : MaskValue(element.Value))));

            default:
                return value;
        }
    }

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*")]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"\b(password|pwd|secret|token)\s*=\s*[^;&\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionSecretPattern();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@([A-Za-z0-9.-]+\.[A-Za-z]{2,})\b")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\+[1-9][0-9]{7,14}\b")]
    private static partial Regex PhonePattern();
}
