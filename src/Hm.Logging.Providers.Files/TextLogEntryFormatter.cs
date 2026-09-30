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
            TextMetadataFormatter.Append(builder, entry.Metadata);
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

}
