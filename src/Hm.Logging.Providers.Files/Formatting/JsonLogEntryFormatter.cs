using System.Buffers;
using System.Text.Json;
using Hm.Logging.Models;
using Hm.Logging.Providers.Shared.Formatting;

namespace Hm.Logging.Providers.Files.Formatting;

internal static class JsonLogEntryFormatter
{
    internal static byte[] Format(LogEntry entry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonLogEntryWriter.Write(writer, entry);
        }

        byte[] output = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(output);
        output[^1] = (byte)'\n';
        return output;
    }
}
