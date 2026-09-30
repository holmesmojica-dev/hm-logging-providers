using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Hm.Logging.Providers.Shared.Formatting;

internal static class TextMetadataFormatter
{
    private static readonly JsonSerializerOptions QuotedTextOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static void Append(StringBuilder builder, ImmutableDictionary<string, object> metadata)
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
            _ = builder.Append(FormatValue(item.Value));
            isFirst = false;
        }
    }

    private static string FormatValue(object? value)
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
