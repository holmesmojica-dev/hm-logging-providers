using Hm.Logging.Abstractions;
using Hm.Logging.Models;
using Hm.Logging.Providers.Files.Configuration;

namespace Hm.Logging.Providers.Files;

/// <summary>
/// Persists normalized HM Logging entries to UTF-8 files with UTC daily naming, rotation, and retention.
/// </summary>
/// <remarks>
/// <para>
/// This provider performs Files-specific serialization and persistence after Core has validated, normalized, and
/// enriched an entry. Most applications should register it through
/// <see cref="Extensions.FilesServiceCollectionExtensions.AddLoggingFiles(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{FilesProviderOptions}?)"/>.
/// </para>
/// <para>
/// Concurrency guarantees are local to one provider instance. The configured directory is provider-managed storage
/// and should not be changed externally while the provider is active.
/// </para>
/// </remarks>
public sealed class FilesProvider : ILogProvider, IDisposable
{
    private readonly object _lifecycleLock = new();
    private readonly FilesProviderSettings _settings;
    private readonly FilesStorage _storage;
    private int _activeWrites;
    private bool _disposed;
    private bool _disposalStarted;

    /// <summary>
    /// Initializes a Files provider and captures its configuration for the provider lifetime.
    /// </summary>
    /// <param name="options">The Files configuration to validate and capture.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option contains an unsupported or zero value.</exception>
    public FilesProvider(FilesProviderOptions options)
        : this(FilesProviderSettings.FromOptions(options), TimeProvider.System)
    {
    }

    internal FilesProvider(FilesProviderOptions options, TimeProvider timeProvider)
        : this(FilesProviderSettings.FromOptions(options), timeProvider, hooks: null)
    {
    }

    internal FilesProvider(FilesProviderOptions options, TimeProvider timeProvider, FilesStorageHooks hooks)
        : this(FilesProviderSettings.FromOptions(options), timeProvider, hooks)
    {
    }

    internal FilesProvider(FilesProviderSettings settings, TimeProvider timeProvider)
        : this(settings, timeProvider, hooks: null)
    {
    }

    private FilesProvider(FilesProviderSettings settings, TimeProvider timeProvider, FilesStorageHooks? hooks)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _storage = new FilesStorage(settings, timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)), hooks);
    }

    /// <summary>
    /// Serializes and appends one complete normalized entry to the applicable Files-managed stream.
    /// </summary>
    /// <param name="entry">The normalized entry supplied by HM Logging Core.</param>
    /// <param name="cancellationToken">A token that cancels waiting for or performing destination work.</param>
    /// <returns>A task that completes after the complete entry has been written through the provider stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an entry cannot satisfy the configured per-file or total-storage limit.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when disposal has started.</exception>
    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        BeginWrite();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] content = FilesLogEntryFormatter.Format(entry, _settings.Format);
            await _storage.WriteAsync(entry.Source, content, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>
    /// Stops admitting writes, waits for admitted writes to finish, and releases synchronization resources.
    /// </summary>
    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_disposalStarted)
            {
                while (!_disposed)
                {
                    _ = Monitor.Wait(_lifecycleLock);
                }

                return;
            }

            _disposalStarted = true;
            while (_activeWrites > 0)
            {
                _ = Monitor.Wait(_lifecycleLock);
            }

            try
            {
                _storage.Dispose();
            }
            finally
            {
                _disposed = true;
                Monitor.PulseAll(_lifecycleLock);
            }
        }
    }

    private void BeginWrite()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposalStarted, this);
            _activeWrites++;
        }
    }

    private void EndWrite()
    {
        lock (_lifecycleLock)
        {
            _activeWrites--;
            if (_activeWrites == 0)
            {
                Monitor.PulseAll(_lifecycleLock);
            }
        }
    }
}
