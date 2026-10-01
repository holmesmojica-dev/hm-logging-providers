using Hm.Logging.Models;
using Hm.Logging.Providers.Files.Configuration;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class FilesMaintenanceTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 28, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExistingCurrentDayStorageAboveTotalLimitIsRejectedWithoutDeletion()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        LogEntry entry = TestEntries.Create("cannot fit");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        string current = Path.Combine(directory.Path, "logs-2026-09-28.jsonl");
        await File.WriteAllBytesAsync(current, new byte[checked((int)entrySize + 1)], TestContext.Current.CancellationToken);
        using var provider = new FilesProvider(
            CreateOptions(directory.Path, retentionDays: null, maximumTotalSize: FileSize.FromBytes(entrySize)),
            new TestTimeProvider(UtcNow));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WriteAsync(entry, TestContext.Current.CancellationToken));

        Assert.Contains("current UTC day", exception.Message, StringComparison.Ordinal);
        Assert.Equal(checked((long)entrySize + 1), new FileInfo(current).Length);
    }

    [Fact]
    public async Task RetentionDeletesOnlyEligibleOldDaysAndPreservesCurrentAndFutureFiles()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string old = Path.Combine(directory.Path, "logs-2026-09-26.jsonl");
        string current = Path.Combine(directory.Path, "logs-2026-09-28.jsonl");
        string future = Path.Combine(directory.Path, "logs-2026-09-29.jsonl");
        foreach (string path in new[] { old, current, future })
        {
            await File.WriteAllTextAsync(path, "existing", TestContext.Current.CancellationToken);
        }

        using var provider = new FilesProvider(
            CreateOptions(directory.Path, retentionDays: 2, maximumTotalSize: null),
            new TestTimeProvider(UtcNow));

        await provider.WriteAsync(TestEntries.Create("current"), TestContext.Current.CancellationToken);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(current));
        Assert.True(File.Exists(future));
    }

    [Fact]
    public async Task RetentionHonorsCancellationBeforeDeletingManagedFiles()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string first = Path.Combine(directory.Path, "logs-2026-09-25.jsonl");
        string second = Path.Combine(directory.Path, "logs-2026-09-26.jsonl");
        await File.WriteAllTextAsync(first, "first", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(second, "second", TestContext.Current.CancellationToken);
        var settings = FilesProviderSettings.FromOptions(
            CreateOptions(directory.Path, retentionDays: 1, maximumTotalSize: null));
        using var maintenance = new FilesStorageMaintenance(settings);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            maintenance.Reconcile(new DateOnly(2026, 9, 28), cancellation.Token));

        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task CapacityLeaseRejectsDoubleCompletionAndAllowsRepeatedDisposal()
    {
        using var directory = new TemporaryDirectory();
        var settings = FilesProviderSettings.FromOptions(
            CreateOptions(directory.Path, retentionDays: null, maximumTotalSize: FileSize.FromBytes(2)));
        using var maintenance = new FilesStorageMaintenance(settings);
        maintenance.Reconcile(new DateOnly(2026, 9, 28), TestContext.Current.CancellationToken);
        FilesStorageMaintenance.CapacityLease lease = Assert.IsType<FilesStorageMaintenance.CapacityLease>(
            await maintenance.AcquireCapacityAsync(1, new DateOnly(2026, 9, 28), TestContext.Current.CancellationToken));

        lease.Complete();
        _ = Assert.Throws<InvalidOperationException>(lease.Complete);
        lease.Dispose();
        lease.Dispose();

        FilesStorageMaintenance.CapacityLease second = Assert.IsType<FilesStorageMaintenance.CapacityLease>(
            await maintenance.AcquireCapacityAsync(1, new DateOnly(2026, 9, 28), TestContext.Current.CancellationToken));
        second.Dispose();
    }

    private static FilesProviderOptions CreateOptions(
        string directory,
        uint? retentionDays,
        FileSize? maximumTotalSize)
    {
        return new FilesProviderOptions
        {
            DirectoryPath = directory,
            MaximumFileSize = null,
            MaximumTotalSize = maximumTotalSize,
            RetentionDays = retentionDays
        };
    }
}
