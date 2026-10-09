using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Hm.Logging.Enums;
using Hm.Logging.Models;
using Hm.Logging.Providers.Shared.Formatting;

namespace Hm.Logging.Providers.Files.Formatting;

internal static class ClefLogEntryFormatter
{
    private const int CompatibleTraceIdLength = 32;

    internal static byte[] Format(LogEntry entry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteEvent(writer, entry);
        }

        byte[] output = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(output);
        output[^1] = (byte)'\n';
        return output;
    }

    private static void WriteEvent(Utf8JsonWriter writer, LogEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("@t", entry.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("@m", entry.Message);
        writer.WriteString("@l", FormatLevel(entry.Level));
        WriteOptionalString(writer, "@x", entry.Exception);
        WriteTraceId(writer, entry.TraceId);
        WriteOptionalString(writer, nameof(entry.CorrelationId), entry.CorrelationId);
        WriteOptionalString(writer, nameof(entry.Source), entry.Source);

        if (entry.Metadata is { Count: > 0 })
        {
            writer.WritePropertyName(nameof(entry.Metadata));
            JsonLogEntryWriter.WriteMetadata(writer, entry.Metadata);
        }

        writer.WriteEndObject();
    }

    private static string FormatLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "Verbose",
            LogLevel.Debug => "Debug",
            LogLevel.Information => "Information",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            LogLevel.Critical => "Fatal",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, $"Unsupported log level '{level}'.")
        };
    }

    private static void WriteTraceId(Utf8JsonWriter writer, string? traceId)
    {
        if (traceId is null)
        {
            return;
        }

        if (IsCompatibleTraceId(traceId))
        {
            writer.WriteString("@tr", traceId.ToLowerInvariant());
        }
        else
        {
            writer.WriteString(nameof(LogEntry.TraceId), traceId);
        }
    }

    private static bool IsCompatibleTraceId(string traceId)
    {
        if (traceId.Length != CompatibleTraceIdLength)
        {
            return false;
        }

        bool containsNonZeroDigit = false;
        foreach (char character in traceId)
        {
            if (!IsHexadecimal(character))
            {
                return false;
            }

            containsNonZeroDigit |= character != '0';
        }

        return containsNonZeroDigit;
    }

    private static bool IsHexadecimal(char character)
    {
        return character is (>= '0' and <= '9')
            or (>= 'a' and <= 'f')
            or (>= 'A' and <= 'F');
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }
}
