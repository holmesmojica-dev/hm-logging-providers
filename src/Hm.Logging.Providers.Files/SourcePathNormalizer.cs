using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;

namespace Hm.Logging.Providers.Files;

internal static class SourcePathNormalizer
{
    private const int MaximumPrefixLength = 80;
    private static readonly FrozenSet<string> WindowsReservedNames = new[]
    {
        "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static string Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "unknown";
        }

        string normalized = source.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        bool pendingSeparator = false;
        foreach (char character in normalized)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    _ = builder.Append('-');
                }

                _ = builder.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        string result = builder.ToString();
        if (result.Length == 0)
        {
            return $"source-{GetStableSuffix(normalized)}";
        }

        if (WindowsReservedNames.Contains(result))
        {
            result = $"source-{result}";
        }

        if (result.Length > MaximumPrefixLength)
        {
            int prefixLength = MaximumPrefixLength - 13;
            result = $"{result[..prefixLength]}-{GetStableSuffix(normalized)}";
        }

        return result;
    }

    private static string GetStableSuffix(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }
}
