using Hm.Logging.Models;
using Hm.Logging.Providers.Files.Configuration;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class FilesConcurrencyTests
{
    private static readonly TimeSpan ConcurrencyTimeout = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 28, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CancelledStreamWaitReleasesRegistrationAndKeepsCoordinatorUsable()
    {
        var secondRequestObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requestCount = 0;
        using var coordinator = new FilesWriteCoordinator(
            new TestTimeProvider(UtcNow),
            new FilesStorageHooks(StreamLockRequested: streamKey =>
            {
                if (Interlocked.Increment(ref requestCount) == 2)
                {
                    _ = secondRequestObserved.TrySetResult();
                }
            }));
        FilesWriteCoordinator.WriteLease first = await AcquireAsync(coordinator, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        Task<FilesWriteCoordinator.WriteLease> second = AcquireAsync(coordinator, cancellation.Token);

        try
        {
            await secondRequestObserved.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
            cancellation.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
            Assert.Equal(1, coordinator.StreamLockCount);
        }
        finally
        {
            await first.DisposeAsync();
        }

        Assert.Equal(0, coordinator.StreamLockCount);
        FilesWriteCoordinator.WriteLease subsequent = await AcquireAsync(coordinator, TestContext.Current.CancellationToken);
        await subsequent.DisposeAsync();
        await subsequent.DisposeAsync();
        Assert.Equal(0, coordinator.StreamLockCount);
    }

    [Fact]
    public async Task ConcurrentDisposalWaitsForAdmittedWriteAndCompletesIdempotently()
    {
        using var directory = new TemporaryDirectory();
        var appendEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAppend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstDisposeInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDisposeInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new FilesStorageHooks(BeforeAppendAsync: async (path, cancellationToken) =>
        {
            _ = appendEntered.TrySetResult();
            await releaseAppend.Task.WaitAsync(cancellationToken);
        });
        var provider = new FilesProvider(
            CreateOptions(directory.Path),
            new TestTimeProvider(UtcNow),
            hooks);
        Task write = provider.WriteAsync(TestEntries.Create("blocked"), TestContext.Current.CancellationToken);

        await appendEntered.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
        var firstDispose = Task.Run(() =>
        {
            _ = firstDisposeInvoked.TrySetResult();
            provider.Dispose();
        }, TestContext.Current.CancellationToken);
        await firstDisposeInvoked.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
        var secondDispose = Task.Run(() =>
        {
            _ = secondDisposeInvoked.TrySetResult();
            provider.Dispose();
        }, TestContext.Current.CancellationToken);

        try
        {
            await secondDisposeInvoked.Task.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
            Assert.False(write.IsCompleted);
            Assert.False(firstDispose.IsCompleted);
            Assert.False(secondDispose.IsCompleted);
        }
        finally
        {
            _ = releaseAppend.TrySetResult();
        }

        await write.WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(ConcurrencyTimeout, TestContext.Current.CancellationToken);
        provider.Dispose();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            provider.WriteAsync(TestEntries.Create("after disposal"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AppendFailureAfterCapacityAdmissionInvalidatesAndReconcilesStorage()
    {
        using var directory = new TemporaryDirectory();
        LogEntry entry = TestEntries.Create("capacity recovery");
        ulong entrySize = (ulong)JsonLogEntryFormatter.Format(entry).Length;
        int appendCount = 0;
        int maintenanceCount = 0;
        var hooks = new FilesStorageHooks(
            MaintenanceWaitingForWrites: _ => Interlocked.Increment(ref maintenanceCount),
            BeforeAppendAsync: (_, _) => Interlocked.Increment(ref appendCount) == 1
                ? Task.FromException(new IOException("Injected append failure."))
                : Task.CompletedTask);
        using var provider = new FilesProvider(
            CreateOptions(directory.Path, FileSize.FromBytes(entrySize)),
            new TestTimeProvider(UtcNow),
            hooks);

        IOException exception = await Assert.ThrowsAsync<IOException>(() =>
            provider.WriteAsync(entry, TestContext.Current.CancellationToken));

        Assert.Equal("Injected append failure.", exception.Message);
        Assert.Empty(Directory.GetFiles(directory.Path));
        await provider.WriteAsync(entry, TestContext.Current.CancellationToken);

        Assert.Equal(2, Volatile.Read(ref maintenanceCount));
        Assert.Equal(2, Volatile.Read(ref appendCount));
        Assert.Equal(
            (long)entrySize,
            new FileInfo(Path.Combine(directory.Path, "logs-2026-09-28.jsonl")).Length);
    }

    private static Task<FilesWriteCoordinator.WriteLease> AcquireAsync(
        FilesWriteCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        return coordinator.AcquireWriteAsync(
            static dateUtc => $"logs-{dateUtc:yyyy-MM-dd}",
            static (_, _) => { },
            cancellationToken);
    }

    private static FilesProviderOptions CreateOptions(string directory, FileSize? maximumTotalSize = null)
    {
        return new FilesProviderOptions
        {
            DirectoryPath = directory,
            MaximumFileSize = null,
            MaximumTotalSize = maximumTotalSize,
            RetentionDays = null
        };
    }
}
