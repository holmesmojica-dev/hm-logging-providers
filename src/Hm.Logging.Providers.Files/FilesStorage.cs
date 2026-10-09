using System.Globalization;
using Hm.Logging.Providers.Files.Configuration;

namespace Hm.Logging.Providers.Files;

internal sealed class FilesStorage : IDisposable
{
    private readonly FilesStorageHooks? _hooks;
    private readonly FilesStorageMaintenance _maintenance;
    private readonly FilesProviderSettings _settings;
    private readonly FilesWriteCoordinator _writeCoordinator;

    internal FilesStorage(
        FilesProviderSettings settings,
        TimeProvider timeProvider,
        FilesStorageHooks? hooks = null)
    {
        _settings = settings;
        _hooks = hooks;
        _maintenance = new FilesStorageMaintenance(settings);
        _writeCoordinator = new FilesWriteCoordinator(timeProvider, hooks);
    }

    internal async Task WriteAsync(string? source, byte[] content, CancellationToken cancellationToken)
    {
        AssertEntryFitsFile(content);
        string prefix = _settings.GroupBySource ? SourcePathNormalizer.Normalize(source) : "logs";
        string directory = _settings.GroupBySource
            ? Path.Combine(_settings.DirectoryPath, prefix)
            : _settings.DirectoryPath;

        await using FilesWriteCoordinator.WriteLease writeLease = await _writeCoordinator.AcquireWriteAsync(
            dateUtc => ResolveStreamKey(directory, prefix, dateUtc),
            _maintenance.Reconcile,
            cancellationToken).ConfigureAwait(false);
        using FilesStorageMaintenance.CapacityLease? capacityLease = await _maintenance.AcquireCapacityAsync(
            (ulong)content.Length,
            writeLease.DateUtc,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await WriteToStreamAsync(directory, prefix, writeLease.DateUtc, content, cancellationToken).ConfigureAwait(false);
            capacityLease?.Complete();
        }
        catch
        {
            if (capacityLease is not null)
            {
                _writeCoordinator.InvalidateMaintenance();
            }

            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _writeCoordinator.Dispose();
        }
        finally
        {
            _maintenance.Dispose();
        }
    }

    private async Task WriteToStreamAsync(
        string directory,
        string prefix,
        DateOnly dateUtc,
        byte[] content,
        CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(directory);
        string path = SelectSegment(directory, prefix, dateUtc, (ulong)content.Length);
        if (_hooks?.BeforeAppendAsync is { } beforeAppendAsync)
        {
            await beforeAppendAsync(path, cancellationToken).ConfigureAwait(false);
        }

        await using var stream = new FileStream(
            path,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long originalLength = stream.Seek(0, SeekOrigin.End);
        try
        {
            if (_hooks?.AppendAsync is { } appendAsync)
            {
                await appendAsync(stream, content, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            var originalException =
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            try
            {
                stream.SetLength(originalLength);
            }
            finally
            {
                originalException.Throw();
            }
        }
    }

    private string SelectSegment(string directory, string prefix, DateOnly dateUtc, ulong entrySize)
    {
        string extension = GetExtension();
        string date = dateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        IReadOnlyList<ManagedLogFile> candidates = [.. Directory.EnumerateFiles(directory)
            .Select(path => ManagedLogFile.TryParse(_settings.DirectoryPath, path, out ManagedLogFile? file) ? file : null)
            .Where(file => file is not null
                && file.DateUtc == dateUtc
                && string.Equals(file.Prefix, prefix, StringComparison.Ordinal)
                && string.Equals(file.Extension, extension, StringComparison.Ordinal))
            .Cast<ManagedLogFile>()
            .OrderBy(file => file.Segment)];

        if (_settings.MaximumFileSize is null)
        {
            return Path.Combine(directory, $"{prefix}-{date}.{extension}");
        }

        ManagedLogFile? current = candidates.Count == 0 ? null : candidates[^1];
        if (current is null)
        {
            return Path.Combine(directory, $"{prefix}-{date}.{extension}");
        }

        ulong maximum = _settings.MaximumFileSize.Value.Bytes;
        if (current.Length <= maximum && entrySize <= maximum - current.Length)
        {
            return current.Path;
        }

        int nextSegment = checked(current.Segment + 1);
        return Path.Combine(directory, $"{prefix}-{date}.{nextSegment.ToString(CultureInfo.InvariantCulture)}.{extension}");
    }

    private void AssertEntryFitsFile(byte[] content)
    {
        if (_settings.MaximumFileSize is FileSize maximum && (ulong)content.Length > maximum.Bytes)
        {
            throw new InvalidOperationException(
                $"The serialized log entry size of {content.Length} bytes exceeds MaximumFileSize of {maximum.Bytes} bytes.");
        }
    }

    private string ResolveStreamKey(string directory, string prefix, DateOnly dateUtc)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{directory}|{prefix}|{dateUtc:yyyy-MM-dd}|{GetExtension()}");
    }

    private string GetExtension()
    {
        return _settings.Format switch
        {
            FilesLogFormat.Json => "jsonl",
            FilesLogFormat.Text => "log",
            FilesLogFormat.Clef => "clef",
            _ => throw new InvalidOperationException(
                $"The Files provider option 'Format' has unsupported value '{_settings.Format}'.")
        };
    }
}

internal sealed record FilesStorageHooks(
    Action<string>? StreamLockRequested = null,
    Action<DateOnly>? MaintenanceWaitingForWrites = null,
    Func<string, CancellationToken, Task>? BeforeAppendAsync = null,
    Func<FileStream, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? AppendAsync = null);
