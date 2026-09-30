using System.Text;
using Hm.Logging.Models;

namespace Hm.Logging.Providers.Shared.Formatting;

internal static class TextLogEntryWriter
{
    internal static void Write(
        StringBuilder builder,
        LogEntry entry,
        string timestamp,
        string level,
        string? exception)
    {
        _ = builder.Append('[');
        _ = builder.Append(timestamp);
        _ = builder.Append("] [");
        _ = builder.Append(level);
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

        AppendOptionalLine(builder, "Exception", exception);
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
