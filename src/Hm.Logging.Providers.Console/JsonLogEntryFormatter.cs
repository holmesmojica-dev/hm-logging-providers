using System.Buffers;
using System.Text;
using System.Text.Json;
using Hm.Logging.Models;
using Hm.Logging.Providers.Shared.Formatting;

namespace Hm.Logging.Providers.Console;

internal static class JsonLogEntryFormatter
{
    internal static string Format(LogEntry entry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonLogEntryWriter.Write(writer, entry);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
