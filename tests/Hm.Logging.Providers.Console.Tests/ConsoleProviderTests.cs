using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Hm.Logging.Enums;
using Hm.Logging.Models;
using Hm.Logging.Providers.Console.Configuration;
using Xunit;

namespace Hm.Logging.Providers.Console.Tests;

public sealed class ConsoleProviderTests
{
    private static readonly DateTime Timestamp = new(2026, 9, 27, 14, 5, 6, 789, DateTimeKind.Utc);

    [Fact]
    public void OptionsHaveDocumentedDefaults()
    {
        var options = new ConsoleProviderOptions();

        Assert.Equal(ConsoleOutputFormat.Text, options.Format);
        Assert.Equal(ConsoleTimestampFormat.DateTime, options.TimestampFormat);
        Assert.True(options.UseColors);
        Assert.True(options.UseStandardErrorForErrors);
        Assert.Equal(ConsoleExceptionFormat.Multiline, options.ExceptionFormat);
    }

    [Fact]
    public async Task TextOutputUsesOrderedSectionsAndOmitsAbsentValues()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.UseColors = false);
        LogEntry entry = CreateEntry() with
        {
            Source = "Checkout",
            TraceId = "trace-1",
            CorrelationId = "order-2",
            Metadata = Metadata(("region", "north")),
            Exception = "failure"
        };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string output = Assert.Single(writer.Writes).Value;
        Assert.StartsWith("[2026-09-27 14:05:06] [Information] [Checkout] message", output);
        Assert.True(output.IndexOf("TraceId: trace-1", StringComparison.Ordinal)
            < output.IndexOf("CorrelationId: order-2", StringComparison.Ordinal));
        Assert.True(output.IndexOf("CorrelationId: order-2", StringComparison.Ordinal)
            < output.IndexOf("Metadata: region=\"north\"", StringComparison.Ordinal));
        Assert.True(output.IndexOf("Metadata:", StringComparison.Ordinal)
            < output.IndexOf("Exception: failure", StringComparison.Ordinal));
        Assert.DoesNotContain("Source: null", output);
    }

    [Fact]
    public async Task TextOutputOmitsAllMissingOptionalSections()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.UseColors = false);

        await provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken);

        string output = Assert.Single(writer.Writes).Value;
        Assert.Equal($"[2026-09-27 14:05:06] [Information] message{Environment.NewLine}", output);
        Assert.DoesNotContain("TraceId", output);
        Assert.DoesNotContain("CorrelationId", output);
        Assert.DoesNotContain("Metadata", output);
        Assert.DoesNotContain("Exception", output);
    }

    [Theory]
    [InlineData(ConsoleTimestampFormat.Iso8601, "2026-09-27T14:05:06.7890000Z")]
    [InlineData(ConsoleTimestampFormat.DateTime, "2026-09-27 14:05:06")]
    [InlineData(ConsoleTimestampFormat.Time, "14:05:06.789")]
    public async Task TextOutputHonorsTimestampFormat(ConsoleTimestampFormat format, string expected)
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options =>
        {
            options.TimestampFormat = format;
            options.UseColors = false;
        });

        await provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken);

        Assert.StartsWith($"[{expected}]", Assert.Single(writer.Writes).Value);
    }

    [Fact]
    public async Task JsonOutputUsesContractPropertyNamesSemanticLevelAndIsoTimestamp()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options =>
        {
            options.Format = ConsoleOutputFormat.Json;
            options.TimestampFormat = ConsoleTimestampFormat.Time;
            options.UseColors = true;
            options.ExceptionFormat = ConsoleExceptionFormat.Compact;
        });
        LogEntry entry = CreateEntry() with
        {
            Level = LogLevel.Error,
            Source = "Worker",
            TraceId = "trace",
            CorrelationId = "correlation",
            Exception = "first\r\nsecond"
        };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string output = Assert.Single(writer.Writes).Value;
        using var document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        Assert.Equal("message", root.GetProperty("Message").GetString());
        Assert.Equal("Error", root.GetProperty("Level").GetString());
        Assert.Equal("2026-09-27T14:05:06.7890000Z", root.GetProperty("Timestamp").GetString());
        Assert.Equal("Worker", root.GetProperty("Source").GetString());
        Assert.Equal("trace", root.GetProperty("TraceId").GetString());
        Assert.Equal("correlation", root.GetProperty("CorrelationId").GetString());
        Assert.Equal("first\r\nsecond", root.GetProperty("Exception").GetString());
        Assert.DoesNotContain('\u001b', output);
    }

    [Fact]
    public async Task JsonOutputOmitsMissingOptionalPropertiesAndEmptyMetadata()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.Format = ConsoleOutputFormat.Json);
        LogEntry entry = CreateEntry() with { Metadata = [] };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(Assert.Single(writer.Writes).Value);
        JsonElement root = document.RootElement;
        Assert.False(root.TryGetProperty("Source", out _));
        Assert.False(root.TryGetProperty("TraceId", out _));
        Assert.False(root.TryGetProperty("CorrelationId", out _));
        Assert.False(root.TryGetProperty("Exception", out _));
        Assert.False(root.TryGetProperty("Metadata", out _));
    }

    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, false)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    public async Task DefaultRoutingUsesStandardErrorOnlyForErrorAndCritical(LogLevel level, bool standardError)
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer);

        await provider.WriteAsync(CreateEntry() with { Level = level }, TestContext.Current.CancellationToken);

        Assert.Equal(standardError, Assert.Single(writer.Writes).StandardError);
    }

    [Theory]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public async Task RoutingCanSendErrorsToStandardOutput(LogLevel level)
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.UseStandardErrorForErrors = false);

        await provider.WriteAsync(CreateEntry() with { Level = level }, TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(writer.Writes).StandardError);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "\u001b[90m")]
    [InlineData(LogLevel.Debug, "\u001b[36m")]
    [InlineData(LogLevel.Information, "\u001b[32m")]
    [InlineData(LogLevel.Warning, "\u001b[33m")]
    [InlineData(LogLevel.Error, "\u001b[31m")]
    [InlineData(LogLevel.Critical, "\u001b[91m")]
    public async Task TextColorsUseFixedPaletteAndResetImmediately(LogLevel level, string color)
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer);

        await provider.WriteAsync(CreateEntry() with { Level = level }, TestContext.Current.CancellationToken);

        string output = Assert.Single(writer.Writes).Value;
        Assert.Contains($"[{color}{level}\u001b[0m]", output);
        Assert.DoesNotContain($"{color}message", output);
    }

    [Fact]
    public async Task TextColorsCanBeDisabled()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.UseColors = false);

        await provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain('\u001b', Assert.Single(writer.Writes).Value);
    }

    [Fact]
    public async Task ExceptionFormatCanPreserveOrCompactOnlyTheException()
    {
        var multilineWriter = new RecordingConsoleWriter();
        using ConsoleProvider multiline = CreateProvider(multilineWriter, options => options.UseColors = false);
        var compactWriter = new RecordingConsoleWriter();
        using ConsoleProvider compact = CreateProvider(compactWriter, options =>
        {
            options.UseColors = false;
            options.ExceptionFormat = ConsoleExceptionFormat.Compact;
        });
        LogEntry entry = CreateEntry() with { TraceId = "trace", Exception = "one\r\ntwo\rthree\nfour" };

        await multiline.WriteAsync(entry, TestContext.Current.CancellationToken);
        await compact.WriteAsync(entry, TestContext.Current.CancellationToken);

        Assert.Contains("Exception: one\r\ntwo\rthree\nfour", Assert.Single(multilineWriter.Writes).Value);
        string compactOutput = Assert.Single(compactWriter.Writes).Value;
        Assert.Contains($"TraceId: trace{Environment.NewLine}Exception: one | two | three | four", compactOutput);
    }

    [Fact]
    public async Task TextMetadataIsOrdinalDeterministicTypedAndCultureIndependent()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var writer = new RecordingConsoleWriter();
            using ConsoleProvider provider = CreateProvider(writer, options => options.UseColors = false);
            var dateTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var dateTimeOffset = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));
            LogEntry entry = CreateEntry() with
            {
                Metadata = Metadata(
                    ("zText", "quoted \"value\""),
                    ("aBool", true),
                    ("bDecimal", 1234.50m),
                    ("cDouble", 12.5d),
                    ("dDateTime", dateTime),
                    ("eOffset", dateTimeOffset),
                    ("fSpan", TimeSpan.FromMinutes(90)),
                    ("gInteger", 7),
                    ("hSingle", 2.5f),
                    ("iCharacter", 'x'))
            };

            await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

            string output = Assert.Single(writer.Writes).Value;
            Assert.Contains(
                "Metadata: aBool=true, bDecimal=1234.50, cDouble=12.5, "
                + "dDateTime=2026-01-02T03:04:05.0000000Z, "
                + "eOffset=2026-01-02T03:04:05.0000000+05:30, "
                + "fSpan=01:30:00, gInteger=7, hSingle=2.5, iCharacter=\"x\", "
                + "zText=\"quoted \\\"value\\\"\"",
                output);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task JsonMetadataPreservesTypesTemporalFormatsAndOrdinalOrder()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.Format = ConsoleOutputFormat.Json);
        var dateTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var dateTimeOffset = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-4));
        LogEntry entry = CreateEntry() with
        {
            Metadata = Metadata(
                ("zString", "42"),
                ("aBoolean", true),
                ("bByte", (byte)1),
                ("cSByte", (sbyte)-2),
                ("dShort", (short)-3),
                ("eUShort", (ushort)4),
                ("fInteger", 42),
                ("gUInteger", 43u),
                ("hLong", -44L),
                ("iULong", 45UL),
                ("jDecimal", 4.25m),
                ("kSingle", 5.5f),
                ("lDouble", 6.75d),
                ("mCharacter", 'x'),
                ("nDateTime", dateTime),
                ("oOffset", dateTimeOffset),
                ("pSpan", TimeSpan.FromSeconds(2)),
                ("qNative", new IntPtr(7)),
                ("rUnsignedNative", new UIntPtr(8)))
        };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(Assert.Single(writer.Writes).Value);
        JsonElement metadata = document.RootElement.GetProperty("Metadata");
        Assert.Equal(
            [
                "aBoolean", "bByte", "cSByte", "dShort", "eUShort", "fInteger", "gUInteger",
                "hLong", "iULong", "jDecimal", "kSingle", "lDouble", "mCharacter", "nDateTime",
                "oOffset", "pSpan", "qNative", "rUnsignedNative", "zString"
            ],
            [.. metadata.EnumerateObject().Select(property => property.Name)]);
        Assert.True(metadata.GetProperty("aBoolean").GetBoolean());
        Assert.Equal(1, metadata.GetProperty("bByte").GetByte());
        Assert.Equal(-2, metadata.GetProperty("cSByte").GetSByte());
        Assert.Equal(-3, metadata.GetProperty("dShort").GetInt16());
        Assert.Equal(4, metadata.GetProperty("eUShort").GetUInt16());
        Assert.Equal(42, metadata.GetProperty("fInteger").GetInt32());
        Assert.Equal(43u, metadata.GetProperty("gUInteger").GetUInt32());
        Assert.Equal(-44L, metadata.GetProperty("hLong").GetInt64());
        Assert.Equal(45UL, metadata.GetProperty("iULong").GetUInt64());
        Assert.Equal(4.25m, metadata.GetProperty("jDecimal").GetDecimal());
        Assert.Equal(5.5f, metadata.GetProperty("kSingle").GetSingle());
        Assert.Equal(6.75d, metadata.GetProperty("lDouble").GetDouble());
        Assert.Equal("x", metadata.GetProperty("mCharacter").GetString());
        Assert.Equal("2026-01-02T03:04:05.0000000Z", metadata.GetProperty("nDateTime").GetString());
        Assert.Equal("2026-01-02T03:04:05.0000000-04:00", metadata.GetProperty("oOffset").GetString());
        Assert.Equal("00:00:02", metadata.GetProperty("pSpan").GetString());
        Assert.Equal(7, metadata.GetProperty("qNative").GetInt64());
        Assert.Equal(8UL, metadata.GetProperty("rUnsignedNative").GetUInt64());
        Assert.Equal(JsonValueKind.String, metadata.GetProperty("zString").ValueKind);
    }

    [Fact]
    public async Task JsonMetadataSafelyAdaptsNonFiniteFloatingPointValues()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.Format = ConsoleOutputFormat.Json);
        LogEntry entry = CreateEntry() with
        {
            Metadata = Metadata(
                ("nan", double.NaN),
                ("positive", float.PositiveInfinity),
                ("negative", double.NegativeInfinity))
        };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(Assert.Single(writer.Writes).Value);
        JsonElement metadata = document.RootElement.GetProperty("Metadata");
        Assert.Equal("NaN", metadata.GetProperty("nan").GetString());
        Assert.Equal("Infinity", metadata.GetProperty("positive").GetString());
        Assert.Equal("-Infinity", metadata.GetProperty("negative").GetString());
    }

    [Theory]
    [InlineData("Format", 17)]
    [InlineData("TimestampFormat", 18)]
    [InlineData("ExceptionFormat", 19)]
    public void InvalidEnumConfigurationFailsClearly(string optionName, int value)
    {
        var options = new ConsoleProviderOptions();
        switch (optionName)
        {
            case "Format":
                options.Format = (ConsoleOutputFormat)value;
                break;
            case "TimestampFormat":
                options.TimestampFormat = (ConsoleTimestampFormat)value;
                break;
            case "ExceptionFormat":
                options.ExceptionFormat = (ConsoleExceptionFormat)value;
                break;
            default:
                break;
        }

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConsoleProvider(options));

        Assert.Contains(optionName, exception.Message);
        Assert.Contains(value.ToString(CultureInfo.InvariantCulture), exception.Message);
        Assert.Contains("Console", exception.Message);
    }

    [Fact]
    public void FormattersFailClosedIfInvalidInternalSettingsReachThem()
    {
        LogEntry entry = CreateEntry() with { Exception = "failure" };

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ConsoleLogEntryFormatter.Format(
            entry,
            new ConsoleProviderSettings(
                (ConsoleOutputFormat)99,
                ConsoleTimestampFormat.DateTime,
                false,
                true,
                ConsoleExceptionFormat.Multiline)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ConsoleLogEntryFormatter.Format(
            entry,
            new ConsoleProviderSettings(
                ConsoleOutputFormat.Text,
                (ConsoleTimestampFormat)99,
                false,
                true,
                ConsoleExceptionFormat.Multiline)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ConsoleLogEntryFormatter.Format(
            entry,
            new ConsoleProviderSettings(
                ConsoleOutputFormat.Text,
                ConsoleTimestampFormat.DateTime,
                false,
                true,
                (ConsoleExceptionFormat)99)));
    }

    [Fact]
    public async Task ProviderSnapshotsConfigurationAtConstruction()
    {
        var options = new ConsoleProviderOptions { UseColors = false };
        var writer = new RecordingConsoleWriter();
        using var provider = new ConsoleProvider(options, writer);
        options.Format = ConsoleOutputFormat.Json;

        await provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken);

        Assert.StartsWith("[2026-09-27", Assert.Single(writer.Writes).Value);
    }

    [Fact]
    public async Task PublicProviderWritesToSystemConsoleStreams()
    {
        TextWriter originalOutput = System.Console.Out;
        TextWriter originalError = System.Console.Error;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        System.Console.SetOut(output);
        System.Console.SetError(error);
        try
        {
            using var provider = new ConsoleProvider(new ConsoleProviderOptions { UseColors = false });

            await provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken);
            await provider.WriteAsync(
                CreateEntry() with { Level = LogLevel.Error, Message = "error" },
                TestContext.Current.CancellationToken);
        }
        finally
        {
            System.Console.SetOut(originalOutput);
            System.Console.SetError(originalError);
        }

        Assert.Contains("message", output.ToString());
        Assert.Contains("error", error.ToString());
    }

    [Fact]
    public async Task ProviderCanBeDisposedMoreThanOnce()
    {
        var provider = new ConsoleProvider(new ConsoleProviderOptions(), new RecordingConsoleWriter());

        provider.Dispose();
        provider.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProviderHonorsCancellationBeforeWriting()
    {
        var writer = new RecordingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.WriteAsync(CreateEntry(), cancellation.Token));

        Assert.Empty(writer.Writes);
    }

    [Fact]
    public async Task ProviderPropagatesDestinationFailures()
    {
        var expected = new IOException("console unavailable");
        using var provider = new ConsoleProvider(new ConsoleProviderOptions(), new ThrowingConsoleWriter(expected));

        IOException actual = await Assert.ThrowsAsync<IOException>(
            () => provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ConcurrentWritesToSameStreamNeverOverlap()
    {
        var writer = new ConcurrencyTrackingConsoleWriter();
        using ConsoleProvider provider = CreateProvider(writer, options => options.UseColors = false);
        Task[] writes = [.. Enumerable.Range(0, 24)
            .Select(index => provider.WriteAsync(
                CreateEntry() with { Message = $"message-{index}" },
                TestContext.Current.CancellationToken))];

        await Task.WhenAll(writes);

        Assert.Equal(1, writer.MaximumConcurrentStandardOutputWrites);
        Assert.Equal(24, writer.Writes.Count);
        Assert.All(writer.Writes, output => Assert.Equal(1, CountOccurrences(output, "message-")));
    }

    [Theory]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Error, true)]
    public async Task DisposeWaitsForActiveWriteAndRejectsNewWrites(LogLevel level, bool standardError)
    {
        var writer = new BlockingConsoleWriter();
        var provider = new ConsoleProvider(new ConsoleProviderOptions { UseColors = false }, writer);
        Task activeWrite = provider.WriteAsync(
            CreateEntry() with { Level = level },
            TestContext.Current.CancellationToken);
        await writer.WaitUntilEnteredAsync(standardError, TestContext.Current.CancellationToken);

        var dispose = Task.Run(provider.Dispose, TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(() => provider.IsDisposalStarted, TimeSpan.FromSeconds(5)));
        var concurrentDispose = Task.Run(provider.Dispose, TestContext.Current.CancellationToken);
        Assert.False(dispose.IsCompleted);
        Assert.False(concurrentDispose.IsCompleted);

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            provider.WriteAsync(CreateEntry(), TestContext.Current.CancellationToken));

        writer.Release(standardError);
        await activeWrite;
        await Task.WhenAll(dispose, concurrentDispose);

        provider.Dispose();
    }

    [Fact]
    public async Task StandardOutputAndErrorRemainIndependentDuringConcurrentWrites()
    {
        var writer = new BlockingConsoleWriter();
        using var provider = new ConsoleProvider(
            new ConsoleProviderOptions { UseColors = false },
            writer);

        Task standardOutputWrite = provider.WriteAsync(
            CreateEntry(),
            TestContext.Current.CancellationToken);
        await writer.WaitUntilEnteredAsync(false, TestContext.Current.CancellationToken);

        Task standardErrorWrite = provider.WriteAsync(
            CreateEntry() with { Level = LogLevel.Error },
            TestContext.Current.CancellationToken);
        await writer.WaitUntilEnteredAsync(true, TestContext.Current.CancellationToken);

        writer.Release(false);
        writer.Release(true);
        await Task.WhenAll(standardOutputWrite, standardErrorWrite);
    }

    private static ConsoleProvider CreateProvider(
        IConsoleWriter writer,
        Action<ConsoleProviderOptions>? configure = null)
    {
        var options = new ConsoleProviderOptions();
        configure?.Invoke(options);
        return new ConsoleProvider(options, writer);
    }

    private static LogEntry CreateEntry()
    {
        return new LogEntry
        {
            Message = "message",
            Level = LogLevel.Information,
            Timestamp = Timestamp
        };
    }

    private static ImmutableDictionary<string, object> Metadata(params (string Key, object Value)[] values)
    {
        return values.ToImmutableDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private sealed class RecordingConsoleWriter : IConsoleWriter
    {
        public ConcurrentQueue<ConsoleWrite> Writes { get; } = new();

        public ValueTask WriteAsync(bool standardError, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Enqueue(new ConsoleWrite(standardError, value));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingConsoleWriter(Exception exception) : IConsoleWriter
    {
        public ValueTask WriteAsync(bool standardError, string value, CancellationToken cancellationToken)
        {
            return ValueTask.FromException(exception);
        }
    }

    private sealed class ConcurrencyTrackingConsoleWriter : IConsoleWriter
    {
        private int _activeStandardOutputWrites;
        private int _maximumConcurrentStandardOutputWrites;

        public ConcurrentQueue<string> Writes { get; } = new();

        public int MaximumConcurrentStandardOutputWrites => _maximumConcurrentStandardOutputWrites;

        public async ValueTask WriteAsync(bool standardError, string value, CancellationToken cancellationToken)
        {
            Assert.False(standardError);
            int activeWrites = Interlocked.Increment(ref _activeStandardOutputWrites);
            UpdateMaximum(activeWrites);
            try
            {
#pragma warning disable xUnit1051 // The test writer must exercise the provider-supplied token.
                await Task.Delay(5, cancellationToken);
#pragma warning restore xUnit1051
                Writes.Enqueue(value);
            }
            finally
            {
                _ = Interlocked.Decrement(ref _activeStandardOutputWrites);
            }
        }

        private void UpdateMaximum(int candidate)
        {
            int current;
            do
            {
                current = _maximumConcurrentStandardOutputWrites;
                if (candidate <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _maximumConcurrentStandardOutputWrites, candidate, current) != current);
        }
    }

    private sealed class BlockingConsoleWriter : IConsoleWriter
    {
        private readonly TaskCompletionSource _standardErrorEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _standardErrorRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _standardOutputEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _standardOutputRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask WriteAsync(bool standardError, string value, CancellationToken cancellationToken)
        {
            TaskCompletionSource entered = standardError ? _standardErrorEntered : _standardOutputEntered;
            TaskCompletionSource release = standardError ? _standardErrorRelease : _standardOutputRelease;
            _ = entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }

        public Task WaitUntilEnteredAsync(bool standardError, CancellationToken cancellationToken)
        {
            TaskCompletionSource entered = standardError ? _standardErrorEntered : _standardOutputEntered;
            return entered.Task.WaitAsync(cancellationToken);
        }

        public void Release(bool standardError)
        {
            TaskCompletionSource release = standardError ? _standardErrorRelease : _standardOutputRelease;
            _ = release.TrySetResult();
        }
    }

    private sealed record ConsoleWrite(bool StandardError, string Value);
}
