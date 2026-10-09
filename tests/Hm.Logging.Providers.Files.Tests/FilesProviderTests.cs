using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Hm.Logging.Enums;
using Hm.Logging.Models;
using Hm.Logging.Providers.Files.Configuration;
using Hm.Logging.Providers.Files.Formatting;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class FilesProviderTests
{
    private static readonly TimeSpan ConcurrencyTimeout = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 28, 10, 30, 0, TimeSpan.Zero);
    private static readonly int[] MetadataArray = [1, 2];

    [Fact]
    public async Task DefaultOutputIsBomlessJsonLinesUsingCurrentUtcDay()
    {
        using var directory = new TemporaryDirectory();
        var clock = new TestTimeProvider(UtcNow);
        using FilesProvider provider = CreateProvider(directory.Path, clock);
        LogEntry entry = TestEntries.Create("Order accepted", "checkout") with
        {
            Level = LogLevel.Warning,
            TraceId = "trace-1",
            CorrelationId = "order-7",
            Exception = "diagnostic",
            Metadata = ImmutableDictionary<string, object>.Empty
                .Add("duration", 12)
                .Add("nonFinite", double.PositiveInfinity)
        };

        Assert.False(Directory.Exists(directory.Path));
        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string path = Path.Combine(directory.Path, "logs-2026-09-28.jsonl");
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Equal(1, bytes.Count(value => value == (byte)'\n'));
        using var document = JsonDocument.Parse(bytes.AsMemory(0, bytes.Length - 1));
        JsonElement root = document.RootElement;
        Assert.Equal("Order accepted", root.GetProperty("Message").GetString());
        Assert.Equal("Warning", root.GetProperty("Level").GetString());
        Assert.Equal("2001-02-03T04:05:06.0000000Z", root.GetProperty("Timestamp").GetString());
        Assert.Equal("checkout", root.GetProperty("Source").GetString());
        Assert.Equal("trace-1", root.GetProperty("TraceId").GetString());
        Assert.Equal("order-7", root.GetProperty("CorrelationId").GetString());
        Assert.Equal("diagnostic", root.GetProperty("Exception").GetString());
        Assert.Equal(12, root.GetProperty("Metadata").GetProperty("duration").GetInt32());
        Assert.Equal("Infinity", root.GetProperty("Metadata").GetProperty("nonFinite").GetString());
    }

    [Fact]
    public async Task JsonOutputPreservesSupportedMetadataTypesDeterministically()
    {
        using var directory = new TemporaryDirectory();
        using FilesProvider provider = CreateProvider(directory.Path);
        LogEntry entry = TestEntries.Create("metadata") with
        {
            Metadata = ImmutableDictionary<string, object>.Empty
                .Add("array", MetadataArray)
                .Add("boolean", true)
                .Add("byte", (byte)1)
                .Add("character", 'x')
                .Add("dateTime", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
                .Add("dateTimeOffset", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5)))
                .Add("decimal", 1.25m)
                .Add("double", 2.5d)
                .Add("int16", (short)-2)
                .Add("int32", -3)
                .Add("int64", -4L)
                .Add("intPtr", new IntPtr(-5))
                .Add("negativeInfinity", double.NegativeInfinity)
                .Add("notANumber", float.NaN)
                .Add("null", null!)
                .Add("sbyte", (sbyte)-6)
                .Add("single", 3.5f)
                .Add("string", "value")
                .Add("timeSpan", TimeSpan.FromSeconds(7))
                .Add("uint16", (ushort)8)
                .Add("uint32", 9U)
                .Add("uint64", 10UL)
                .Add("uintPtr", new UIntPtr(11))
        };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string line = await File.ReadAllTextAsync(
            Path.Combine(directory.Path, "logs-2026-09-28.jsonl"),
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(line);
        JsonElement metadata = document.RootElement.GetProperty("Metadata");
        Assert.Equal([1, 2], [.. metadata.GetProperty("array").EnumerateArray().Select(value => value.GetInt32())]);
        Assert.True(metadata.GetProperty("boolean").GetBoolean());
        Assert.Equal("x", metadata.GetProperty("character").GetString());
        Assert.Equal("2026-01-02T03:04:05.0000000Z", metadata.GetProperty("dateTime").GetString());
        Assert.Equal("2026-01-02T03:04:05.0000000-05:00", metadata.GetProperty("dateTimeOffset").GetString());
        Assert.Equal(1.25m, metadata.GetProperty("decimal").GetDecimal());
        Assert.Equal(-5, metadata.GetProperty("intPtr").GetInt64());
        Assert.Equal("-Infinity", metadata.GetProperty("negativeInfinity").GetString());
        Assert.Equal("NaN", metadata.GetProperty("notANumber").GetString());
        Assert.Equal(JsonValueKind.Null, metadata.GetProperty("null").ValueKind);
        Assert.Equal("00:00:07", metadata.GetProperty("timeSpan").GetString());
        Assert.Equal(11UL, metadata.GetProperty("uintPtr").GetUInt64());
        Assert.Equal(
            metadata.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal),
            metadata.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task TextOutputUsesLogExtensionAndPreservesRelevantEntryInformation()
    {
        using var directory = new TemporaryDirectory();
        using FilesProvider provider = CreateProvider(directory.Path, format: FilesLogFormat.Text);
        LogEntry entry = TestEntries.Create("Text message", "worker") with
        {
            TraceId = "trace",
            CorrelationId = "correlation",
            Exception = "line one\nline two",
            Metadata = ImmutableDictionary<string, object>.Empty.Add("region", "north")
        };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string content = await File.ReadAllTextAsync(
            Path.Combine(directory.Path, "logs-2026-09-28.log"),
            TestContext.Current.CancellationToken);
        Assert.Contains("[2001-02-03T04:05:06.0000000Z] [Information] [worker] Text message", content);
        Assert.Contains("TraceId: trace", content);
        Assert.Contains("CorrelationId: correlation", content);
        Assert.Contains("Metadata: region=\"north\"", content);
        Assert.Contains("Exception: line one\nline two", content.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SourceDoesNotAffectPhysicalStorageUnlessGroupingIsEnabled()
    {
        using var directory = new TemporaryDirectory();
        using (FilesProvider provider = CreateProvider(directory.Path))
        {
            await provider.WriteAsync(TestEntries.Create("one", "payments"), TestContext.Current.CancellationToken);
            await provider.WriteAsync(TestEntries.Create("two", "catalog"), TestContext.Current.CancellationToken);
        }

        _ = Assert.Single(Directory.GetFiles(directory.Path));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "payments")));
    }

    [Theory]
    [InlineData(" Payments API ", "payments-api")]
    [InlineData("../../Secrets\\Windows", "secrets-windows")]
    [InlineData("CON", "source-con")]
    [InlineData("***", null)]
    [InlineData(null, "unknown")]
    [InlineData(" ", "unknown")]
    public void SourceNormalizationIsSafeAndDeterministic(string? source, string? expected)
    {
        string first = SourcePathNormalizer.Normalize(source);
        string second = SourcePathNormalizer.Normalize(source);

        Assert.Equal(first, second);
        Assert.DoesNotContain("..", first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('\\', first);
        if (expected is not null)
        {
            Assert.Equal(expected, first);
        }
        else
        {
            Assert.StartsWith("source-", first, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SourceNormalizationBoundsLongNamesAndSafelyRepresentsUnicodeOnlySources()
    {
        string longSource = SourcePathNormalizer.Normalize(new string('a', 200));
        string unicodeSource = SourcePathNormalizer.Normalize("支付");

        Assert.Equal(80, longSource.Length);
        Assert.StartsWith(new string('a', 67), longSource, StringComparison.Ordinal);
        Assert.StartsWith("source-", unicodeSource, StringComparison.Ordinal);
        Assert.NotEqual("unknown", unicodeSource);
    }

    [Fact]
    public async Task GroupingCreatesSafeSourceContainersAndUnknownFallback()
    {
        using var directory = new TemporaryDirectory();
        using FilesProvider provider = CreateProvider(directory.Path, groupBySource: true);

        await provider.WriteAsync(TestEntries.Create("one", "Payments API"), TestContext.Current.CancellationToken);
        await provider.WriteAsync(TestEntries.Create("two"), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(directory.Path, "payments-api", "payments-api-2026-09-28.jsonl")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "unknown", "unknown-2026-09-28.jsonl")));
    }

    [Fact]
    public async Task StrictFileLimitRotatesBeforeAnEntryWouldExceedIt()
    {
        using var directory = new TemporaryDirectory();
        LogEntry entry = TestEntries.Create("rotation");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(directory.Path, maximumFileSize: FileSize.FromBytes(entrySize));

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);
        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string first = Path.Combine(directory.Path, "logs-2026-09-28.jsonl");
        string second = Path.Combine(directory.Path, "logs-2026-09-28.1.jsonl");
        Assert.Equal((long)entrySize, new FileInfo(first).Length);
        Assert.Equal((long)entrySize, new FileInfo(second).Length);
    }

    [Fact]
    public async Task EntryLargerThanStrictFileLimitIsRejectedWithoutCreatingAnOversizedFile()
    {
        using var directory = new TemporaryDirectory();
        LogEntry entry = TestEntries.Create("too large");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(directory.Path, maximumFileSize: FileSize.FromBytes(entrySize - 1));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WriteAsync(entry, TestContext.Current.CancellationToken));

        Assert.Contains("MaximumFileSize", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(directory.Path));
    }

    [Fact]
    public async Task NullMaximumFileSizeDisablesSegmentation()
    {
        using var directory = new TemporaryDirectory();
        using FilesProvider provider = CreateProvider(directory.Path, maximumFileSize: null, setMaximumFileSize: true);

        await provider.WriteAsync(TestEntries.Create(new string('x', 1_000)), TestContext.Current.CancellationToken);
        await provider.WriteAsync(TestEntries.Create(new string('y', 1_000)), TestContext.Current.CancellationToken);

        _ = Assert.Single(Directory.GetFiles(directory.Path, "*.jsonl"));
    }

    [Fact]
    public async Task DailyMaintenanceDeletesExpiredWholeDaysAndPreservesForeignFiles()
    {
        using var directory = new TemporaryDirectory();
        string sourceDirectory = Path.Combine(directory.Path, "payments");
        _ = Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "payments-2026-09-26.jsonl"), "old", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "payments-2026-09-26.1.jsonl"), "old", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "logs-2026-09-27.log"), "retained", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "notes.txt"), "foreign", TestContext.Current.CancellationToken);
        using FilesProvider provider = CreateProvider(directory.Path, groupBySource: true, retentionDays: 2);

        await provider.WriteAsync(TestEntries.Create("new", "catalog"), TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(sourceDirectory));
        Assert.True(File.Exists(Path.Combine(directory.Path, "logs-2026-09-27.log")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "catalog", "catalog-2026-09-28.jsonl")));
    }

    [Fact]
    public async Task RetentionPreservesForeignFilesInsideSourceDirectory()
    {
        using var directory = new TemporaryDirectory();
        string sourceDirectory = Path.Combine(directory.Path, "payments");
        _ = Directory.CreateDirectory(sourceDirectory);
        string expired = Path.Combine(sourceDirectory, "payments-2026-09-26.jsonl");
        string foreign = Path.Combine(sourceDirectory, "keep.txt");
        await File.WriteAllTextAsync(expired, "old", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(foreign, "keep", TestContext.Current.CancellationToken);
        using FilesProvider provider = CreateProvider(directory.Path, groupBySource: true, retentionDays: 2);

        await provider.WriteAsync(TestEntries.Create("new", "catalog"), TestContext.Current.CancellationToken);

        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(foreign));
        Assert.True(Directory.Exists(sourceDirectory));
    }

    [Fact]
    public async Task RetentionCanBeDisabled()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string old = Path.Combine(directory.Path, "logs-2020-01-01.jsonl");
        await File.WriteAllTextAsync(old, "old", TestContext.Current.CancellationToken);
        using FilesProvider provider = CreateProvider(directory.Path, retentionDays: null);

        await provider.WriteAsync(TestEntries.Create(), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(old));
    }

    [Fact]
    public async Task TotalLimitDeletesOldestEligibleDaysBeforeWriting()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string oldest = Path.Combine(directory.Path, "logs-2026-09-25.jsonl");
        string newer = Path.Combine(directory.Path, "logs-2026-09-27.jsonl");
        await File.WriteAllBytesAsync(oldest, new byte[11], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(newer, new byte[7], TestContext.Current.CancellationToken);
        LogEntry entry = TestEntries.Create("capacity");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(
            directory.Path,
            retentionDays: null,
            maximumFileSize: null,
            setMaximumFileSize: true,
            maximumTotalSize: FileSize.FromBytes(entrySize + 7));

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(newer));
        Assert.Equal((long)entrySize, new FileInfo(Path.Combine(directory.Path, "logs-2026-09-28.jsonl")).Length);
    }

    [Fact]
    public async Task TotalLimitHandlesOldFileGrowthAfterReconciliation()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string old = Path.Combine(directory.Path, "logs-2026-09-27.jsonl");
        await File.WriteAllBytesAsync(old, [0], TestContext.Current.CancellationToken);
        LogEntry entry = TestEntries.Create("capacity after external growth");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        int growthCount = 0;
        var hooks = new FilesStorageHooks(StreamLockRequested: _ =>
        {
            if (Interlocked.Increment(ref growthCount) == 1)
            {
                File.WriteAllBytes(old, [0, 1]);
            }
        });
        using FilesProvider provider = CreateProvider(
            directory.Path,
            retentionDays: null,
            maximumFileSize: null,
            setMaximumFileSize: true,
            maximumTotalSize: FileSize.FromBytes(entrySize),
            hooks: hooks);

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref growthCount));
        Assert.False(File.Exists(old));
        Assert.Equal(
            (long)entrySize,
            new FileInfo(Path.Combine(directory.Path, "logs-2026-09-28.jsonl")).Length);
    }

    [Fact]
    public async Task TotalLimitNeverDeletesCurrentDayToAdmitAnEntry()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string current = Path.Combine(directory.Path, "logs-2026-09-28.jsonl");
        await File.WriteAllBytesAsync(current, new byte[5], TestContext.Current.CancellationToken);
        LogEntry entry = TestEntries.Create("capacity");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(
            directory.Path,
            retentionDays: null,
            maximumFileSize: null,
            setMaximumFileSize: true,
            maximumTotalSize: FileSize.FromBytes(entrySize));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WriteAsync(entry, TestContext.Current.CancellationToken));

        Assert.Contains("current UTC day", exception.Message, StringComparison.Ordinal);
        Assert.Equal(5, new FileInfo(current).Length);
    }

    [Fact]
    public async Task TotalLimitCoordinatesConcurrentCapacityAdmission()
    {
        using var directory = new TemporaryDirectory();
        LogEntry entry = TestEntries.Create("same-size");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(
            directory.Path,
            retentionDays: null,
            maximumFileSize: null,
            setMaximumFileSize: true,
            maximumTotalSize: FileSize.FromBytes(entrySize));
        Task first = provider.WriteAsync(entry, TestContext.Current.CancellationToken);
        Task second = provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.WhenAll(first, second));

        Assert.Equal(1, new[] { first, second }.Count(task => task.IsCompletedSuccessfully));
        Assert.Equal(1, new[] { first, second }.Count(task => task.IsFaulted));
        Assert.Equal((long)entrySize, new FileInfo(Path.Combine(directory.Path, "logs-2026-09-28.jsonl")).Length);
    }

    [Fact]
    public async Task TotalLimitRejectsAnEntryThatCannotFitByItself()
    {
        using var directory = new TemporaryDirectory();
        LogEntry entry = TestEntries.Create("capacity");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(
            directory.Path,
            maximumFileSize: null,
            setMaximumFileSize: true,
            maximumTotalSize: FileSize.FromBytes(entrySize - 1));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WriteAsync(entry, TestContext.Current.CancellationToken));

        Assert.Contains("MaximumTotalSize", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task ConcurrentWritesToSameStreamRemainCompleteJsonLines()
    {
        using var directory = new TemporaryDirectory();
        using FilesProvider provider = CreateProvider(directory.Path, maximumFileSize: null, setMaximumFileSize: true);
        Task[] writes = [.. Enumerable.Range(0, 100).Select(index => provider.WriteAsync(TestEntries.Create($"message-{index}"), TestContext.Current.CancellationToken))];

        await Task.WhenAll(writes);

        string[] lines = await File.ReadAllLinesAsync(
            Path.Combine(directory.Path, "logs-2026-09-28.jsonl"),
            TestContext.Current.CancellationToken);
        Assert.Equal(100, lines.Length);
        Assert.Equal(100, lines.Select(line => JsonDocument.Parse(line).RootElement.GetProperty("Message").GetString()).Distinct().Count());
    }

    [Fact]
    public async Task SameStreamWriterWaitsUntilPhysicalStreamOwnerReleasesIt()
    {
        using var directory = new TemporaryDirectory();
        var firstAppendEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstAppend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRequestObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int appendCount = 0;
        int requestCount = 0;
        var hooks = new FilesStorageHooks(
            StreamLockRequested: _streamKey =>
            {
                if (Interlocked.Increment(ref requestCount) == 2)
                {
                    _ = secondRequestObserved.TrySetResult();
                }
            },
            BeforeAppendAsync: async (_path, cancellationToken) =>
            {
                if (Interlocked.Increment(ref appendCount) == 1)
                {
                    _ = firstAppendEntered.TrySetResult();
                    await releaseFirstAppend.Task.WaitAsync(cancellationToken);
                }
            });
        using FilesProvider provider = CreateProvider(directory.Path, hooks: hooks);

        Task first = provider.WriteAsync(TestEntries.Create("first"), TestContext.Current.CancellationToken);
        Task? second = null;
        try
        {
            await firstAppendEntered.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
            second = provider.WriteAsync(TestEntries.Create("second"), TestContext.Current.CancellationToken);
            await secondRequestObserved.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);

            Assert.Equal(1, Volatile.Read(ref appendCount));
        }
        finally
        {
            _ = releaseFirstAppend.TrySetResult();
        }

        Assert.NotNull(second);
        await Task.WhenAll(first, second).WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);

        string[] lines = await File.ReadAllLinesAsync(
            Path.Combine(directory.Path, "logs-2026-09-28.jsonl"),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task CongestedSourceDoesNotBlockIndependentSourceWithoutGlobalCapacity()
    {
        using var directory = new TemporaryDirectory();
        var firstPaymentsAppendEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstPaymentsAppend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPaymentsRequestObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int paymentsAppendCount = 0;
        int paymentsRequestCount = 0;
        var hooks = new FilesStorageHooks(
            StreamLockRequested: streamKey =>
            {
                if (streamKey.Contains("payments", StringComparison.Ordinal)
                    && Interlocked.Increment(ref paymentsRequestCount) == 2)
                {
                    _ = secondPaymentsRequestObserved.TrySetResult();
                }
            },
            BeforeAppendAsync: async (path, cancellationToken) =>
            {
                if (path.Contains("payments", StringComparison.Ordinal)
                    && Interlocked.Increment(ref paymentsAppendCount) == 1)
                {
                    _ = firstPaymentsAppendEntered.TrySetResult();
                    await releaseFirstPaymentsAppend.Task.WaitAsync(cancellationToken);
                }
            });
        using FilesProvider provider = CreateProvider(directory.Path, groupBySource: true, hooks: hooks);

        Task firstPayments = provider.WriteAsync(TestEntries.Create("first", "payments"), TestContext.Current.CancellationToken);
        Task? secondPayments = null;
        try
        {
            await firstPaymentsAppendEntered.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
            secondPayments = provider.WriteAsync(TestEntries.Create("second", "payments"), TestContext.Current.CancellationToken);
            await secondPaymentsRequestObserved.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
            Task catalog = provider.WriteAsync(TestEntries.Create("independent", "catalog"), TestContext.Current.CancellationToken);

            await catalog.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(Path.Combine(directory.Path, "catalog", "catalog-2026-09-28.jsonl")));
            Assert.False(firstPayments.IsCompleted);
            Assert.False(secondPayments.IsCompleted);
        }
        finally
        {
            _ = releaseFirstPaymentsAppend.TrySetResult();
        }

        Assert.NotNull(secondPayments);
        await Task.WhenAll(firstPayments, secondPayments).WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UtcDayChangeTriggersNewFileAndMaintenance()
    {
        using var directory = new TemporaryDirectory();
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 28, 23, 59, 59, TimeSpan.Zero));
        using FilesProvider provider = CreateProvider(directory.Path, clock, retentionDays: 1);
        await provider.WriteAsync(TestEntries.Create("first"), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(2));

        await provider.WriteAsync(TestEntries.Create("second"), TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(directory.Path, "logs-2026-09-28.jsonl")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "logs-2026-09-29.jsonl")));
    }

    [Fact]
    public async Task UtcMaintenanceWaitsForAdmittedPreviousDayWrite()
    {
        using var directory = new TemporaryDirectory();
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 28, 23, 59, 59, TimeSpan.Zero));
        var previousDayAppendEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePreviousDayAppend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextDayMaintenanceWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new FilesStorageHooks(
            MaintenanceWaitingForWrites: dateUtc =>
            {
                if (dateUtc == new DateOnly(2026, 9, 29))
                {
                    _ = nextDayMaintenanceWaiting.TrySetResult();
                }
            },
            BeforeAppendAsync: async (path, cancellationToken) =>
            {
                if (path.Contains("2026-09-28", StringComparison.Ordinal))
                {
                    _ = previousDayAppendEntered.TrySetResult();
                    await releasePreviousDayAppend.Task.WaitAsync(cancellationToken);
                }
            });
        using FilesProvider provider = CreateProvider(directory.Path, clock, retentionDays: 1, hooks: hooks);

        Task previousDay = provider.WriteAsync(TestEntries.Create("previous"), TestContext.Current.CancellationToken);
        Task? nextDay = null;
        try
        {
            await previousDayAppendEntered.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(2));
            nextDay = provider.WriteAsync(TestEntries.Create("next"), TestContext.Current.CancellationToken);
            await nextDayMaintenanceWaiting.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);

            Assert.False(nextDay.IsCompleted);
        }
        finally
        {
            _ = releasePreviousDayAppend.TrySetResult();
        }

        Assert.NotNull(nextDay);
        await Task.WhenAll(previousDay, nextDay).WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(directory.Path, "logs-2026-09-28.jsonl")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "logs-2026-09-29.jsonl")));
    }

    [Fact]
    public async Task ObsoleteStreamLocksAreReleasedAfterHighCardinalityWrites()
    {
        using var coordinator = new FilesWriteCoordinator(new TestTimeProvider(UtcNow));

        await Task.WhenAll(Enumerable.Range(0, 100).Select(async index =>
        {
            await using FilesWriteCoordinator.WriteLease lease = await coordinator.AcquireWriteAsync(
                dateUtc => $"stream-{index}-{dateUtc:yyyy-MM-dd}",
                static (_, _) => { },
                TestContext.Current.CancellationToken);
        }));

        Assert.Equal(0, coordinator.StreamLockCount);
    }

    [Fact]
    public async Task CancellationAndDisposalPropagateAtProviderBoundary()
    {
        using var directory = new TemporaryDirectory();
        FilesProvider provider = CreateProvider(directory.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.WriteAsync(TestEntries.Create(), cancellation.Token));
        Assert.False(Directory.Exists(directory.Path));

        provider.Dispose();
        provider.Dispose();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.WriteAsync(TestEntries.Create(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NullEntryIsRejected()
    {
        using var directory = new TemporaryDirectory();
        using FilesProvider provider = CreateProvider(directory.Path);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => provider.WriteAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NullEntryIsRejectedBeforeDisposedLifecycleState()
    {
        using var directory = new TemporaryDirectory();
        FilesProvider provider = CreateProvider(directory.Path);
        provider.Dispose();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            provider.WriteAsync(null!, TestContext.Current.CancellationToken));
    }

    private static FilesProvider CreateProvider(
        string directory,
        TestTimeProvider? clock = null,
        bool groupBySource = false,
        uint? retentionDays = 30,
        FileSize? maximumFileSize = null,
        bool setMaximumFileSize = false,
        FileSize? maximumTotalSize = null,
        FilesLogFormat format = FilesLogFormat.Json,
        FilesStorageHooks? hooks = null)
    {
        var options = new FilesProviderOptions
        {
            DirectoryPath = directory,
            GroupBySource = groupBySource,
            RetentionDays = retentionDays,
            MaximumFileSize = setMaximumFileSize ? maximumFileSize : maximumFileSize ?? FileSize.FromMB(100),
            MaximumTotalSize = maximumTotalSize,
            Format = format
        };
        TimeProvider timeProvider = clock ?? new TestTimeProvider(UtcNow);
        return hooks is null
            ? new FilesProvider(options, timeProvider)
            : new FilesProvider(options, timeProvider, hooks);
    }
}
