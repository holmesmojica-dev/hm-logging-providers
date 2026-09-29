using Hm.Logging.Enums;
using Hm.Logging.Models;

namespace Hm.Logging.Providers.Files.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hm-files-tests-{Guid.NewGuid():N}");
    }

    internal string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    internal void Advance(TimeSpan value)
    {
        _utcNow = _utcNow.Add(value);
    }
}

internal static class TestEntries
{
    internal static LogEntry Create(string message = "Test message", string? source = null)
    {
        return new()
        {
            Message = message,
            Level = LogLevel.Information,
            Timestamp = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            Source = source
        };
    }
}
