using Hm.Logging.Models;
using Hm.Logging.Providers.Files.Configuration;

namespace Hm.Logging.Providers.Files.Formatting;

internal static class FilesLogEntryFormatter
{
    internal static byte[] Format(LogEntry entry, FilesLogFormat format)
    {
        return format switch
        {
            FilesLogFormat.Json => JsonLogEntryFormatter.Format(entry),
            FilesLogFormat.Text => TextLogEntryFormatter.Format(entry),
            FilesLogFormat.Clef => ClefLogEntryFormatter.Format(entry),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                $"The Files provider option 'Format' has unsupported value '{format}'.")
        };
    }
}
