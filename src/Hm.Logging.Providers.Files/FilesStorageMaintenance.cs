using Hm.Logging.Providers.Files.Configuration;

namespace Hm.Logging.Providers.Files;

internal sealed class FilesStorageMaintenance(FilesProviderSettings settings) : IDisposable
{
    private readonly SemaphoreSlim _capacityGate = new(1, 1);
    private readonly FilesProviderSettings _settings = settings;
    private ulong _currentTotalSize;

    internal void Reconcile(DateOnly dateUtc, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(_settings.DirectoryPath);
        IReadOnlyList<ManagedLogFile> managedFiles = ManagedLogFile.Discover(_settings.DirectoryPath);
        if (_settings.RetentionDays is uint retentionDays)
        {
            IReadOnlyList<ManagedLogFile> expired = [.. managedFiles.Where(file => IsExpired(file.DateUtc, dateUtc, retentionDays))];
            DeleteManagedFiles(expired, cancellationToken);
            RemoveEmptySourceDirectories(expired);
            managedFiles = ManagedLogFile.Discover(_settings.DirectoryPath);
        }

        if (_settings.MaximumTotalSize is not null)
        {
            _currentTotalSize = SumLengths(managedFiles);
        }
    }

    internal async Task<CapacityLease?> AcquireCapacityAsync(
        ulong entrySize,
        DateOnly currentDateUtc,
        CancellationToken cancellationToken)
    {
        if (_settings.MaximumTotalSize is null)
        {
            return null;
        }

        await _capacityGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureCapacity(entrySize, currentDateUtc, cancellationToken);
            return new CapacityLease(this, entrySize);
        }
        catch
        {
            _ = _capacityGate.Release();
            throw;
        }
    }

    public void Dispose()
    {
        _capacityGate.Dispose();
    }

    private void EnsureCapacity(ulong entrySize, DateOnly currentDateUtc, CancellationToken cancellationToken)
    {
        ulong maximum = _settings.MaximumTotalSize!.Value.Bytes;
        if (entrySize > maximum)
        {
            throw new InvalidOperationException($"The serialized log entry size of {entrySize} bytes exceeds MaximumTotalSize of {maximum} bytes.");
        }

        if (FitsTotalCapacity(entrySize, maximum))
        {
            return;
        }

        IReadOnlyList<IGrouping<DateOnly, ManagedLogFile>> oldDays = [.. ManagedLogFile.Discover(_settings.DirectoryPath)
            .Where(file => file.DateUtc < currentDateUtc)
            .GroupBy(file => file.DateUtc)
            .OrderBy(group => group.Key)];
        foreach (IGrouping<DateOnly, ManagedLogFile> day in oldDays)
        {
            ManagedLogFile[] files = [.. day];
            ulong deletedSize = SumLengths(files);
            DeleteManagedFiles(files, cancellationToken);
            RemoveEmptySourceDirectories(files);
            _currentTotalSize = deletedSize <= _currentTotalSize ? _currentTotalSize - deletedSize : 0;
            if (FitsTotalCapacity(entrySize, maximum))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"The serialized log entry cannot be admitted within MaximumTotalSize of {maximum} bytes without deleting the current UTC day.");
    }

    private bool FitsTotalCapacity(ulong entrySize, ulong maximum)
    {
        return _currentTotalSize <= maximum && entrySize <= maximum - _currentTotalSize;
    }

    private void CompleteWrite(ulong entrySize)
    {
        _currentTotalSize = checked(_currentTotalSize + entrySize);
    }

    private void ReleaseCapacity()
    {
        _ = _capacityGate.Release();
    }

    private static bool IsExpired(DateOnly fileDate, DateOnly currentDate, uint retentionDays)
    {
        return fileDate < currentDate && (retentionDays == 0 || currentDate.DayNumber - fileDate.DayNumber >= retentionDays);
    }

    private static void DeleteManagedFiles(IEnumerable<ManagedLogFile> files, CancellationToken cancellationToken)
    {
        foreach (ManagedLogFile file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(file.Path);
        }
    }

    private static void RemoveEmptySourceDirectories(IEnumerable<ManagedLogFile> files)
    {
        foreach (string directory in files
                     .Select(file => file.SourceDirectory)
                     .Where(static directory => directory is not null)
                     .Cast<string>()
                     .Distinct(StringComparer.Ordinal)
                     .Where(static directory =>
                         Directory.Exists(directory) &&
                         !Directory.EnumerateFileSystemEntries(directory).Any()))
        {
            Directory.Delete(directory);
        }
    }

    private static ulong SumLengths(IEnumerable<ManagedLogFile> files)
    {
        ulong total = 0;
        foreach (ManagedLogFile file in files)
        {
            total = checked(total + file.Length);
        }

        return total;
    }

    internal sealed class CapacityLease(
        FilesStorageMaintenance owner,
        ulong entrySize) : IDisposable
    {
        private FilesStorageMaintenance? _owner = owner;
        private bool _completed;

        internal void Complete()
        {
            ObjectDisposedException.ThrowIf(_owner is null, this);
            if (_completed)
            {
                throw new InvalidOperationException("The capacity lease has already been completed.");
            }

            _owner.CompleteWrite(entrySize);
            _completed = true;
        }

        public void Dispose()
        {
            FilesStorageMaintenance? currentOwner = Interlocked.Exchange(ref _owner, null);
            currentOwner?.ReleaseCapacity();
        }
    }
}
