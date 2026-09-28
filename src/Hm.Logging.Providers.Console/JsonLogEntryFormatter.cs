using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Hm.Logging.Models;

namespace Hm.Logging.Providers.Console;

internal static class JsonLogEntryFormatter
{
    internal static string Format(LogEntry entry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(entry.Message), entry.Message);
            writer.WriteString(nameof(entry.Level), entry.Level.ToString());
            writer.WriteString(
                nameof(entry.Timestamp),
                entry.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            WriteOptionalString(writer, nameof(entry.Source), entry.Source);
            WriteOptionalString(writer, nameof(entry.TraceId), entry.TraceId);
            WriteOptionalString(writer, nameof(entry.CorrelationId), entry.CorrelationId);
            WriteOptionalString(writer, nameof(entry.Exception), entry.Exception);

            if (entry.Metadata is { Count: > 0 })
            {
                writer.WritePropertyName(nameof(entry.Metadata));
                WriteMetadata(writer, entry.Metadata);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static void WriteMetadata(Utf8JsonWriter writer, ImmutableDictionary<string, object> metadata)
    {
        writer.WriteStartObject();
        foreach (KeyValuePair<string, object> item in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(item.Key);
            WriteMetadataValue(writer, item.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteMetadataValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case char character:
                writer.WriteStringValue(character.ToString());
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case byte number:
                writer.WriteNumberValue(number);
                break;
            case sbyte number:
                writer.WriteNumberValue(number);
                break;
            case short number:
                writer.WriteNumberValue(number);
                break;
            case ushort number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case uint number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            case IntPtr number:
                writer.WriteNumberValue(number.ToInt64());
                break;
            case UIntPtr number:
                writer.WriteNumberValue(number.ToUInt64());
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case float number when float.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            case float number:
                writer.WriteStringValue(FormatNonFinite(number));
                break;
            case double number when double.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteStringValue(FormatNonFinite(number));
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeSpan timeSpan:
                writer.WriteStringValue(timeSpan.ToString("c", CultureInfo.InvariantCulture));
                break;
            default:
                JsonSerializer.Serialize(writer, value, value.GetType());
                break;
        }
    }

    private static string FormatNonFinite(double value)
    {
        return double.IsNaN(value) ? "NaN" : double.IsPositiveInfinity(value) ? "Infinity" : "-Infinity";
    }
}
