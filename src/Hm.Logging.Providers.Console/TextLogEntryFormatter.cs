using System.Globalization;
using System.Text;
using Hm.Logging.Enums;
using Hm.Logging.Models;
using Hm.Logging.Providers.Console.Configuration;
using Hm.Logging.Providers.Shared.Formatting;

namespace Hm.Logging.Providers.Console;

internal static class TextLogEntryFormatter
{
    private const string ResetColor = "\u001b[0m";

    internal static string Format(LogEntry entry, ConsoleProviderSettings settings)
    {
        var builder = new StringBuilder();
        _ = builder.Append('[');
        _ = builder.Append(FormatTimestamp(entry.Timestamp, settings.TimestampFormat));
        _ = builder.Append("] [");
        AppendLevel(builder, entry.Level, settings.UseColors);
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
            TextMetadataFormatter.Append(builder, entry.Metadata);
        }

        if (entry.Exception is not null)
        {
            _ = builder.AppendLine();
            _ = builder.Append("Exception: ");
            _ = builder.Append(FormatException(entry.Exception, settings.ExceptionFormat));
        }

        return builder.ToString();
    }

    private static string FormatTimestamp(DateTime timestamp, ConsoleTimestampFormat format)
    {
        DateTime utcTimestamp = timestamp.ToUniversalTime();
        return format switch
        {
            ConsoleTimestampFormat.Iso8601 => utcTimestamp.ToString("O", CultureInfo.InvariantCulture),
            ConsoleTimestampFormat.DateTime => utcTimestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ConsoleTimestampFormat.Time => utcTimestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                $"The Console provider option 'TimestampFormat' has unsupported value '{format}'.")
        };
    }

    private static void AppendLevel(StringBuilder builder, LogLevel level, bool useColors)
    {
        if (useColors)
        {
            _ = builder.Append(GetLevelColor(level));
        }

        _ = builder.Append(level);

        if (useColors)
        {
            _ = builder.Append(ResetColor);
        }
    }

    private static string GetLevelColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "\u001b[90m",
            LogLevel.Debug => "\u001b[36m",
            LogLevel.Information => "\u001b[32m",
            LogLevel.Warning => "\u001b[33m",
            LogLevel.Error => "\u001b[31m",
            LogLevel.Critical => "\u001b[91m",
            _ => string.Empty
        };
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

    private static string FormatException(string exception, ConsoleExceptionFormat format)
    {
        return format switch
        {
            ConsoleExceptionFormat.Multiline => exception,
            ConsoleExceptionFormat.Compact => exception
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\n", " | ", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                $"The Console provider option 'ExceptionFormat' has unsupported value '{format}'.")
        };
    }
}
