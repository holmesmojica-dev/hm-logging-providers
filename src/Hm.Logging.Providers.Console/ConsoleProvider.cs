using Hm.Logging.Abstractions;
using Hm.Logging.Enums;
using Hm.Logging.Models;
using Hm.Logging.Providers.Console.Configuration;

namespace Hm.Logging.Providers.Console;

/// <summary>
/// Writes normalized HM Logging entries to the process console.
/// </summary>
public sealed class ConsoleProvider : ILogProvider, IDisposable
{
    private readonly IConsoleWriter _writer;
    private readonly ConsoleProviderSettings _settings;
    private readonly SemaphoreSlim _standardOutputLock = new(1, 1);
    private readonly SemaphoreSlim _standardErrorLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new Console provider with the supplied configuration.
    /// </summary>
    /// <param name="options">The provider configuration.</param>
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

    /// <inheritdoc/>
    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

    /// <summary>
    /// Releases synchronization resources owned by this provider.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _standardOutputLock.Dispose();
        _standardErrorLock.Dispose();
        _disposed = true;
    }
}
