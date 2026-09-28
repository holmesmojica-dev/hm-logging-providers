using Hm.Logging.Abstractions;
using Hm.Logging.Enums;
using Hm.Logging.Models;
using Hm.Logging.Providers.Console.Configuration;

namespace Hm.Logging.Providers.Console;

/// <summary>
/// Writes normalized HM Logging entries to the process standard output and standard error streams.
/// </summary>
/// <remarks>
/// <para>
/// This provider performs Console-specific presentation and stream routing after HM Logging Core has
/// validated, normalized, and enriched an entry. Most applications should register it through
/// <see cref="Extensions.ConsoleServiceCollectionExtensions.AddLoggingConsole(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{ConsoleProviderOptions}?)"/>
/// instead of constructing it directly.
/// </para>
/// <para>
/// A provider instance may be used concurrently. Entries targeting the same stream are emitted as complete
/// logical units; standard output and standard error remain independently synchronized.
/// </para>
/// </remarks>
/// <seealso cref="ConsoleProviderOptions"/>
public sealed class ConsoleProvider : ILogProvider, IDisposable
{
    private readonly object _lifecycleLock = new();
    private readonly IConsoleWriter _writer;
    private readonly ConsoleProviderSettings _settings;
    private readonly SemaphoreSlim _standardOutputLock = new(1, 1);
    private readonly SemaphoreSlim _standardErrorLock = new(1, 1);
    private int _activeWrites;
    private bool _disposalStarted;
    private bool _disposed;

    /// <summary>
    /// Initializes a Console provider that writes directly to the process standard streams.
    /// </summary>
    /// <param name="options">The configuration to validate and capture for the provider lifetime.</param>
    /// <remarks>
    /// Direct construction is useful outside dependency injection. Applications using
    /// <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> should normally call
    /// <see cref="Extensions.ConsoleServiceCollectionExtensions.AddLoggingConsole(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{ConsoleProviderOptions}?)"/>.
    /// Changes made to <paramref name="options"/> after construction do not affect this instance.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an enum option has an unsupported value.</exception>
    public ConsoleProvider(ConsoleProviderOptions options)
        : this(ConsoleProviderSettings.FromOptions(options), new SystemConsoleWriter())
    {
    }

    internal ConsoleProvider(ConsoleProviderOptions options, IConsoleWriter writer)
        : this(ConsoleProviderSettings.FromOptions(options), writer)
    {
    }

    internal ConsoleProvider(ConsoleProviderSettings settings, IConsoleWriter writer)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>
    /// Formats and writes a normalized log entry to the configured process standard stream.
    /// </summary>
    /// <param name="entry">The normalized HM Logging entry to write.</param>
    /// <param name="cancellationToken">A token that cancels waiting for or writing to the destination stream.</param>
    /// <returns>A task that completes after the entire entry has been emitted.</returns>
    /// <remarks>
    /// By default, Error and Critical entries are written to standard error and lower levels, including Warning,
    /// are written to standard output. Destination write failures and cancellation are propagated to HM Logging
    /// Core. A write admitted before disposal begins is allowed to finish; writes attempted after disposal begins
    /// fail with <see cref="ObjectDisposedException"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when disposal has started for this provider.</exception>
    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        BeginWrite();
        try
        {
            ArgumentNullException.ThrowIfNull(entry);
            cancellationToken.ThrowIfCancellationRequested();

            string output = ConsoleLogEntryFormatter.Format(entry, _settings) + Environment.NewLine;
            bool standardError = _settings.UseStandardErrorForErrors
                && entry.Level is LogLevel.Error or LogLevel.Critical;
            SemaphoreSlim streamLock = standardError ? _standardErrorLock : _standardOutputLock;

            await streamLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteAsync(standardError, output, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = streamLock.Release();
            }
        }
        finally
        {
            EndWrite();
        }
    }

    /// <summary>
    /// Stops accepting writes, waits for admitted writes to finish, and releases provider resources.
    /// </summary>
    /// <remarks>
    /// Disposal is synchronous, thread-safe, and idempotent. When the provider is registered through dependency
    /// injection, the service provider normally manages this singleton's lifetime and disposal.
    /// </remarks>
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
                _standardOutputLock.Dispose();
            }
            finally
            {
                try
                {
                    _standardErrorLock.Dispose();
                }
                finally
                {
                    _disposed = true;
                    Monitor.PulseAll(_lifecycleLock);
                }
            }
        }
    }

    internal bool IsDisposalStarted => Volatile.Read(ref _disposalStarted);

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
