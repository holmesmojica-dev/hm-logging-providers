using System.Collections.Immutable;
using System.Globalization;
using Hm.Logging.Models;
using Hm.Logging.Providers.Files.Configuration;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class TextLogEntryFormatterTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 28, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TextMetadataUsesInvariantDeterministicRepresentationsAndOmitsNullOptionals()
    {
        using var directory = new TemporaryDirectory();
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            using var provider = new FilesProvider(
                new FilesProviderOptions
                {
                    DirectoryPath = directory.Path,
                    Format = FilesLogFormat.Text
                },
                new TestTimeProvider(UtcNow));
            LogEntry entry = TestEntries.Create("text metadata") with
            {
                Metadata = ImmutableDictionary<string, object>.Empty
                    .Add("booleanFalse", false)
                    .Add("booleanTrue", true)
                    .Add("character", 'x')
                    .Add("dateTime", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
                    .Add("dateTimeOffset", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5)))
                    .Add("decimal", 1.25m)
                    .Add("double", 2.5d)
                    .Add("fallback", new FallbackValue())
                    .Add("integer", -3)
                    .Add("negativeInfinity", double.NegativeInfinity)
                    .Add("notANumber", float.NaN)
                    .Add("null", null!)
                    .Add("nullFallback", new NullFallbackValue())
                    .Add("nullFormattable", new NullFormattableValue())
                    .Add("positiveInfinity", double.PositiveInfinity)
                    .Add("single", 3.5f)
                    .Add("string", "line\n\"quoted\"")
                    .Add("timeSpan", TimeSpan.FromSeconds(7))
            };

            await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

            string content = await File.ReadAllTextAsync(
                Path.Combine(directory.Path, "logs-2026-09-28.log"),
                TestContext.Current.CancellationToken);
            string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Equal(
                "[2001-02-03T04:05:06.0000000Z] [Information] text metadata\n" +
                "Metadata: booleanFalse=false, booleanTrue=true, character=\"x\", " +
                "dateTime=2026-01-02T03:04:05.0000000Z, dateTimeOffset=2026-01-02T03:04:05.0000000-05:00, " +
                "decimal=1.25, double=2.5, fallback=fallback, integer=-3, negativeInfinity=-Infinity, " +
                "notANumber=NaN, null=null, nullFallback=, nullFormattable=, positiveInfinity=Infinity, single=3.5, " +
                "string=\"line\\n\\\"quoted\\\"\", timeSpan=00:00:07\n",
                normalized);
            Assert.DoesNotContain("TraceId:", normalized, StringComparison.Ordinal);
            Assert.DoesNotContain("CorrelationId:", normalized, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception:", normalized, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private sealed class FallbackValue
    {
        public override string ToString()
        {
            return "fallback";
        }
    }

    private sealed class NullFallbackValue
    {
        public override string ToString()
        {
            return null!;
        }
    }

    private sealed class NullFormattableValue : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            return null!;
        }
    }
}
