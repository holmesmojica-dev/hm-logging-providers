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
        TextLogEntryWriter.Write(
            builder,
            entry,
            FormatTimestamp(entry.Timestamp, settings.TimestampFormat),
            FormatLevel(entry.Level, settings.UseColors),
            entry.Exception is null ? null : FormatException(entry.Exception, settings.ExceptionFormat));

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

    private static string FormatLevel(LogLevel level, bool useColors)
    {
        return useColors
            ? $"{GetLevelColor(level)}{level}{ResetColor}"
            : level.ToString();
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
