using Hm.Logging.Models;
using Hm.Logging.Providers.Console.Configuration;

namespace Hm.Logging.Providers.Console;

internal static class ConsoleLogEntryFormatter
{
    internal static string Format(LogEntry entry, ConsoleProviderSettings settings)
    {
        return settings.Format switch
        {
            ConsoleOutputFormat.Text => TextLogEntryFormatter.Format(entry, settings),
            ConsoleOutputFormat.Json => JsonLogEntryFormatter.Format(entry),
            _ => throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.Format,
                $"The Console provider option 'Format' has unsupported value '{settings.Format}'.")
        };
    }
}
