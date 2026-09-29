using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Hm.Logging.Models;

namespace Hm.Logging.Providers.Files;

internal static class TextLogEntryFormatter
{
    private static readonly JsonSerializerOptions QuotedTextOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static byte[] Format(LogEntry entry)
    {
        var builder = new StringBuilder();
        _ = builder.Append('[');
        _ = builder.Append(entry.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        _ = builder.Append("] [");
        _ = builder.Append(entry.Level);
        _ = builder.Append(']');

        if (entry.Source is not null)
        {
            _ = builder.Append(" [");
            _ = builder.Append(entry.Source);
            _ = builder.Append(']');
        }

        _ = builder.Append(' ');
        _ = builder.Append(entry.Message);
        AppendOptionalLine(builder, "TraceId", entry.TraceId);
        AppendOptionalLine(builder, "CorrelationId", entry.CorrelationId);

        if (entry.Metadata is { Count: > 0 })
        {
            _ = builder.AppendLine();
            _ = builder.Append("Metadata: ");
            AppendMetadata(builder, entry.Metadata);
        }

        AppendOptionalLine(builder, "Exception", entry.Exception);
        _ = builder.Append('\n');
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendOptionalLine(StringBuilder builder, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        _ = builder.AppendLine();
        _ = builder.Append(name);
        _ = builder.Append(": ");
        _ = builder.Append(value);
    }

    private static void AppendMetadata(StringBuilder builder, ImmutableDictionary<string, object> metadata)
    {
        bool isFirst = true;
        foreach (KeyValuePair<string, object> item in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!isFirst)
            {
                _ = builder.Append(", ");
            }

            _ = builder.Append(item.Key);
            _ = builder.Append('=');
            _ = builder.Append(FormatMetadataValue(item.Value));
            isFirst = false;
        }
    }

    private static string FormatMetadataValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => JsonSerializer.Serialize(text, QuotedTextOptions),
            char character => JsonSerializer.Serialize(character.ToString(), QuotedTextOptions),
            bool boolean => boolean ? "true" : "false",
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            float single => single.ToString("R", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }
}
