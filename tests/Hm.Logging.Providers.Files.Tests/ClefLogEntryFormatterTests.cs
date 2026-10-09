using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Hm.Logging.Abstractions;
using Hm.Logging.Enums;
using Hm.Logging.Extensions;
using Hm.Logging.Models;
using Hm.Logging.Providers.Files.Configuration;
using Hm.Logging.Providers.Files.Formatting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class ClefLogEntryFormatterTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 28, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClefWritesOneValidBomlessJsonEventPerLineWithMappedProperties()
    {
        using var directory = new TemporaryDirectory();
        using FilesProvider provider = CreateProvider(directory.Path);
        const string exception = "System.InvalidOperationException: invalid transition\n   at Checkout.Process()";
        LogEntry entry = TestEntries.Create("Order accepted", "checkout") with
        {
            Level = LogLevel.Error,
            TraceId = "4BF92F3577B34DA6A3CE929D0E0E4736",
            CorrelationId = "order-7",
            Exception = exception,
            Metadata = ImmutableDictionary<string, object>.Empty
                .Add("duration", 12)
                .Add("nonFinite", double.PositiveInfinity)
                .Add("success", false)
        };

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string path = Path.Combine(directory.Path, "logs-2026-09-28.clef");
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Equal(1, bytes.Count(value => value == (byte)'\n'));
        using JsonDocument document = ParseJsonLine(bytes);
        JsonElement root = document.RootElement;
        Assert.Equal("2001-02-03T04:05:06.0000000Z", root.GetProperty("@t").GetString());
        Assert.Equal("Order accepted", root.GetProperty("@m").GetString());
        Assert.Equal("Error", root.GetProperty("@l").GetString());
        Assert.Equal(exception, root.GetProperty("@x").GetString());
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", root.GetProperty("@tr").GetString());
        Assert.Equal("order-7", root.GetProperty("CorrelationId").GetString());
        Assert.Equal("checkout", root.GetProperty("Source").GetString());
        Assert.Equal(12, root.GetProperty("Metadata").GetProperty("duration").GetInt32());
        Assert.Equal("Infinity", root.GetProperty("Metadata").GetProperty("nonFinite").GetString());
        Assert.False(root.GetProperty("Metadata").GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData(LogLevel.Trace, "Verbose")]
    [InlineData(LogLevel.Debug, "Debug")]
    [InlineData(LogLevel.Information, "Information")]
    [InlineData(LogLevel.Warning, "Warning")]
    [InlineData(LogLevel.Error, "Error")]
    [InlineData(LogLevel.Critical, "Fatal")]
    public void ClefMapsHmLevelsToRecognizableClefSeverities(LogLevel level, string expected)
    {
        byte[] bytes = ClefLogEntryFormatter.Format(TestEntries.Create() with { Level = level });

        using JsonDocument document = ParseJsonLine(bytes);
        Assert.Equal(expected, document.RootElement.GetProperty("@l").GetString());
    }

    [Theory]
    [InlineData("trace-1")]
    [InlineData("4bf92f3577b34da6a3ce929d0e0e473g")]
    [InlineData("00000000000000000000000000000000")]
    public void ClefPreservesIncompatibleTraceIdAsAnOrdinaryProperty(string traceId)
    {
        byte[] bytes = ClefLogEntryFormatter.Format(TestEntries.Create() with { TraceId = traceId });

        using JsonDocument document = ParseJsonLine(bytes);
        JsonElement root = document.RootElement;
        Assert.Equal(traceId, root.GetProperty("TraceId").GetString());
        Assert.False(root.TryGetProperty("@tr", out _));
    }

    [Fact]
    public void ClefDoesNotInventUnsupportedReservedProperties()
    {
        byte[] bytes = ClefLogEntryFormatter.Format(TestEntries.Create());

        using JsonDocument document = ParseJsonLine(bytes);
        JsonElement root = document.RootElement;
        foreach (string property in new[] { "@mt", "@i", "@r", "@sp", "@ps", "@st", "@sc", "@ra", "@sk" })
        {
            Assert.False(root.TryGetProperty(property, out _), $"CLEF property '{property}' must not be invented.");
        }

        Assert.False(root.TryGetProperty("@x", out _));
        Assert.False(root.TryGetProperty("@tr", out _));
        Assert.False(root.TryGetProperty("TraceId", out _));
        Assert.False(root.TryGetProperty("CorrelationId", out _));
        Assert.False(root.TryGetProperty("Source", out _));
        Assert.False(root.TryGetProperty("Metadata", out _));
    }

    [Fact]
    public async Task ClefPreservesContextMergedFromNestedCoreScopes()
    {
        using var directory = new TemporaryDirectory();
        var services = new ServiceCollection();
        _ = services
            .AddHmLogging()
            .AddLoggingFiles(options =>
            {
                options.DirectoryPath = directory.Path;
                options.Format = FilesLogFormat.Clef;
            });
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        await using AsyncServiceScope serviceScope = serviceProvider.CreateAsyncScope();
        ILoggerService logger = serviceScope.ServiceProvider.GetRequiredService<ILoggerService>();

        using (logger.BeginScope(new LogContext
        {
            TraceId = "4BF92F3577B34DA6A3CE929D0E0E4736",
            CorrelationId = "order-7",
            Source = "orders",
            Metadata = ImmutableDictionary<string, object>.Empty.Add("tenant", "north")
        }))
        using (logger.BeginScope(new LogContext
        {
            Source = "payments",
            Metadata = ImmutableDictionary<string, object>.Empty.Add("provider", "sample-pay")
        }))
        {
            await logger.LogAsync(LogEntry.Info("Payment authorized"), cancellationToken: TestContext.Current.CancellationToken);
        }

        string path = Assert.Single(Directory.GetFiles(directory.Path, "*.clef"));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("payments", root.GetProperty("Source").GetString());
        Assert.Equal("order-7", root.GetProperty("CorrelationId").GetString());
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", root.GetProperty("@tr").GetString());
        Assert.Equal("north", root.GetProperty("Metadata").GetProperty("tenant").GetString());
        Assert.Equal("sample-pay", root.GetProperty("Metadata").GetProperty("provider").GetString());
        Assert.False(root.TryGetProperty("Scopes", out _));
    }

    [Fact]
    public async Task ClefSupportsStrictRotationAndGroupingBySource()
    {
        using var directory = new TemporaryDirectory();
        LogEntry entry = TestEntries.Create("rotation", "Payments API");
        ulong entrySize = (ulong)ClefLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(
            directory.Path,
            groupBySource: true,
            maximumFileSize: FileSize.FromBytes(entrySize));

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);
        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        string sourceDirectory = Path.Combine(directory.Path, "payments-api");
        string first = Path.Combine(sourceDirectory, "payments-api-2026-09-28.clef");
        string second = Path.Combine(sourceDirectory, "payments-api-2026-09-28.1.clef");
        Assert.Equal((long)entrySize, new FileInfo(first).Length);
        Assert.Equal((long)entrySize, new FileInfo(second).Length);
        using var firstDocument = JsonDocument.Parse(
            await File.ReadAllTextAsync(first, TestContext.Current.CancellationToken));
        using var secondDocument = JsonDocument.Parse(
            await File.ReadAllTextAsync(second, TestContext.Current.CancellationToken));
        Assert.Equal("rotation", firstDocument.RootElement.GetProperty("@m").GetString());
        Assert.Equal("rotation", secondDocument.RootElement.GetProperty("@m").GetString());
    }

    [Fact]
    public async Task JsonAndClefUseDistinctPhysicalFiles()
    {
        using var directory = new TemporaryDirectory();
        using (var jsonProvider = new FilesProvider(
                   new FilesProviderOptions { DirectoryPath = directory.Path },
                   new TestTimeProvider(UtcNow)))
        {
            await jsonProvider.WriteAsync(TestEntries.Create("json"), TestContext.Current.CancellationToken);
        }

        using (FilesProvider clefProvider = CreateProvider(directory.Path))
        {
            await clefProvider.WriteAsync(TestEntries.Create("clef"), TestContext.Current.CancellationToken);
        }

        string jsonPath = Path.Combine(directory.Path, "logs-2026-09-28.jsonl");
        string clefPath = Path.Combine(directory.Path, "logs-2026-09-28.clef");
        using var jsonDocument = JsonDocument.Parse(
            await File.ReadAllTextAsync(jsonPath, TestContext.Current.CancellationToken));
        using var clefDocument = JsonDocument.Parse(
            await File.ReadAllTextAsync(clefPath, TestContext.Current.CancellationToken));
        Assert.Equal("json", jsonDocument.RootElement.GetProperty("Message").GetString());
        Assert.Equal("clef", clefDocument.RootElement.GetProperty("@m").GetString());
    }

    [Fact]
    public async Task ClefRecognizesExistingSegmentsAfterProviderRestart()
    {
        using var directory = new TemporaryDirectory();
        LogEntry entry = TestEntries.Create("restart rotation");
        ulong entrySize = (ulong)ClefLogEntryFormatter.Format(entry).Length;
        using (FilesProvider firstProvider = CreateProvider(
                   directory.Path,
                   maximumFileSize: FileSize.FromBytes(entrySize)))
        {
            await firstProvider.WriteAsync(entry, TestContext.Current.CancellationToken);
        }

        using (FilesProvider restartedProvider = CreateProvider(
                   directory.Path,
                   maximumFileSize: FileSize.FromBytes(entrySize)))
        {
            await restartedProvider.WriteAsync(entry, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, Directory.GetFiles(directory.Path, "*.clef").Length);
        Assert.True(File.Exists(Path.Combine(directory.Path, "logs-2026-09-28.clef")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "logs-2026-09-28.1.clef")));
    }

    [Fact]
    public async Task ClefParticipatesInRetentionWithSourceGrouping()
    {
        using var directory = new TemporaryDirectory();
        string expiredSourceDirectory = Path.Combine(directory.Path, "payments");
        _ = Directory.CreateDirectory(expiredSourceDirectory);
        string expired = Path.Combine(expiredSourceDirectory, "payments-2026-09-26.clef");
        await File.WriteAllTextAsync(expired, "old", TestContext.Current.CancellationToken);
        using FilesProvider provider = CreateProvider(directory.Path, groupBySource: true, retentionDays: 2);

        await provider.WriteAsync(TestEntries.Create("current", "catalog"), TestContext.Current.CancellationToken);

        Assert.False(File.Exists(expired));
        Assert.False(Directory.Exists(expiredSourceDirectory));
        Assert.True(File.Exists(Path.Combine(directory.Path, "catalog", "catalog-2026-09-28.clef")));
    }

    [Fact]
    public async Task ClefParticipatesInTotalCapacityEnforcement()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string old = Path.Combine(directory.Path, "logs-2026-09-27.clef");
        await File.WriteAllBytesAsync(old, [0], TestContext.Current.CancellationToken);
        LogEntry entry = TestEntries.Create("capacity");
        ulong entrySize = (ulong)ClefLogEntryFormatter.Format(entry).Length;
        using FilesProvider provider = CreateProvider(
            directory.Path,
            retentionDays: null,
            maximumTotalSize: FileSize.FromBytes(entrySize));

        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(old));
        Assert.Equal(
            (long)entrySize,
            new FileInfo(Path.Combine(directory.Path, "logs-2026-09-28.clef")).Length);
    }

    private static FilesProvider CreateProvider(
        string directory,
        bool groupBySource = false,
        FileSize? maximumFileSize = null,
        uint? retentionDays = 30,
        FileSize? maximumTotalSize = null)
    {
        var options = new FilesProviderOptions
        {
            DirectoryPath = directory,
            GroupBySource = groupBySource,
            RetentionDays = retentionDays,
            MaximumFileSize = maximumFileSize ?? FileSize.FromMB(100),
            MaximumTotalSize = maximumTotalSize,
            Format = FilesLogFormat.Clef
        };
        return new FilesProvider(options, new TestTimeProvider(UtcNow));
    }

    private static JsonDocument ParseJsonLine(byte[] bytes)
    {
        return JsonDocument.Parse(bytes.AsMemory(0, bytes.Length - 1));
    }
}
