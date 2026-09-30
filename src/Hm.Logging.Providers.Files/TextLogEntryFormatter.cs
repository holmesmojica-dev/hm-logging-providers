using System.Globalization;
using System.Text;
using Hm.Logging.Models;
using Hm.Logging.Providers.Shared.Formatting;

namespace Hm.Logging.Providers.Files;

internal static class TextLogEntryFormatter
{
    internal static byte[] Format(LogEntry entry)
    {
        var builder = new StringBuilder();
        TextLogEntryWriter.Write(
            builder,
            entry,
            entry.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            entry.Level.ToString(),
            entry.Exception);
        _ = builder.Append('\n');
        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}
