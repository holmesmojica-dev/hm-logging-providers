namespace Hm.Logging.Providers.Files;

internal sealed class FilesWriteCoordinator(
    TimeProvider timeProvider,
    FilesStorageHooks? hooks = null) : IDisposable
{
    private readonly Lock _activeWritesLock = new();
    private readonly SemaphoreSlim _admissionGate = new(1, 1);
    private readonly FilesStorageHooks? _hooks = hooks;
    private readonly Dictionary<string, StreamLockEntry> _streamLocks = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider = timeProvider;
    private int _activeWrites;
    private TaskCompletionSource? _activeWritesCompleted;
    private int _maintenanceInvalidated;
    private DateOnly? _lastMaintenanceDateUtc;

    internal int StreamLockCount => _streamLocks.Count;

    internal async Task<WriteLease> AcquireWriteAsync(
        Func<DateOnly, string> resolveStreamKey,
        Action<DateOnly, CancellationToken> reconcileStorage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolveStreamKey);
        ArgumentNullException.ThrowIfNull(reconcileStorage);

        WriteRegistration registration = await RegisterAsync(
            resolveStreamKey,
            reconcileStorage,
            cancellationToken).ConfigureAwait(false);
        try
        {
            _hooks?.StreamLockRequested?.Invoke(registration.StreamKey);
            await registration.StreamLock.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new WriteLease(registration.DateUtc, () => ReleaseAsync(registration));
        }
        catch
        {
            UnregisterActiveWrite();
            await ReleaseStreamLockReferenceAsync(registration.StreamKey, registration.StreamLock).ConfigureAwait(false);
            throw;
        }
    }

    internal void InvalidateMaintenance()
    {
        _ = Interlocked.Exchange(ref _maintenanceInvalidated, 1);
    }

    public void Dispose()
    {
        foreach (StreamLockEntry streamLock in _streamLocks.Values)
        {
            streamLock.Semaphore.Dispose();
        }

        _admissionGate.Dispose();
    }

    private async Task<WriteRegistration> RegisterAsync(
        Func<DateOnly, string> resolveStreamKey,
        Action<DateOnly, CancellationToken> reconcileStorage,
        CancellationToken cancellationToken)
    {
        await _admissionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dateUtc = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            if (_lastMaintenanceDateUtc != dateUtc || Volatile.Read(ref _maintenanceInvalidated) != 0)
            {
                _hooks?.MaintenanceWaitingForWrites?.Invoke(dateUtc);
                await WaitForActiveWritesAsync(cancellationToken).ConfigureAwait(false);
                reconcileStorage(dateUtc, cancellationToken);
                _lastMaintenanceDateUtc = dateUtc;
                Volatile.Write(ref _maintenanceInvalidated, 0);
            }

            string streamKey = resolveStreamKey(dateUtc);
            if (!_streamLocks.TryGetValue(streamKey, out StreamLockEntry? streamLock))
            {
                streamLock = new StreamLockEntry();
                _streamLocks.Add(streamKey, streamLock);
            }

            streamLock.ReferenceCount++;
            RegisterActiveWrite();
            return new WriteRegistration(dateUtc, streamKey, streamLock);
        }
        finally
        {
            _ = _admissionGate.Release();
        }
    }

    private async ValueTask ReleaseAsync(WriteRegistration registration)
    {
        _ = registration.StreamLock.Semaphore.Release();
        UnregisterActiveWrite();
        await ReleaseStreamLockReferenceAsync(registration.StreamKey, registration.StreamLock).ConfigureAwait(false);
    }

    private async Task ReleaseStreamLockReferenceAsync(string streamKey, StreamLockEntry streamLock)
    {
        await _admissionGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            streamLock.ReferenceCount--;
            if (streamLock.ReferenceCount == 0)
            {
                _ = _streamLocks.Remove(streamKey);
                streamLock.Semaphore.Dispose();
            }
        }
        finally
        {
            _ = _admissionGate.Release();
        }
    }

    private void RegisterActiveWrite()
    {
        lock (_activeWritesLock)
        {
            if (_activeWrites == 0)
            {
                _activeWritesCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _activeWrites++;
        }
    }

    private void UnregisterActiveWrite()
    {
        TaskCompletionSource? completed = null;
        lock (_activeWritesLock)
        {
            _activeWrites--;
            if (_activeWrites == 0)
            {
                completed = _activeWritesCompleted;
                _activeWritesCompleted = null;
            }
        }

        _ = completed?.TrySetResult();
    }

    private async Task WaitForActiveWritesAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (_activeWritesLock)
        {
            waitTask = _activeWritesCompleted?.Task ?? Task.CompletedTask;
        }

        await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class StreamLockEntry
    {
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);

        internal int ReferenceCount { get; set; }
    }

    private sealed record WriteRegistration(DateOnly DateUtc, string StreamKey, StreamLockEntry StreamLock);

    internal sealed class WriteLease(
        DateOnly dateUtc,
        Func<ValueTask> releaseAsync) : IAsyncDisposable
    {
        private Func<ValueTask>? _releaseAsync = releaseAsync;

        internal DateOnly DateUtc => dateUtc;

        public ValueTask DisposeAsync()
        {
            Func<ValueTask>? currentRelease = Interlocked.Exchange(ref _releaseAsync, null);
            return currentRelease is null
                ? ValueTask.CompletedTask
                : currentRelease();
        }
    }
}
