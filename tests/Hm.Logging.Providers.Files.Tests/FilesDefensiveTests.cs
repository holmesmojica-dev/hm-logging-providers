using Hm.Logging.Providers.Files.Configuration;
using Hm.Logging.Providers.Files.Formatting;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class FilesDefensiveTests
{
    [Fact]
    public async Task PublicConstructorUsesProductionClockAndWritesSuccessfully()
    {
        using var directory = new TemporaryDirectory();
        var before = DateOnly.FromDateTime(DateTime.UtcNow);
        using var provider = new FilesProvider(new FilesProviderOptions { DirectoryPath = directory.Path });

        await provider.WriteAsync(TestEntries.Create("public constructor"), TestContext.Current.CancellationToken);

        var after = DateOnly.FromDateTime(DateTime.UtcNow);
        string path = Assert.Single(Directory.GetFiles(directory.Path, "*.jsonl"));
        Assert.True(ManagedLogFile.TryParse(directory.Path, path, out ManagedLogFile? managedFile));
        Assert.NotNull(managedFile);
        Assert.InRange(managedFile.DateUtc.DayNumber, before.DayNumber, after.DayNumber);
        Assert.Contains("public constructor", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorsRejectNullDependencies()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new FilesProvider(null!));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new FilesProvider(new FilesProviderOptions(), null!));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new FilesProvider((FilesProviderSettings)null!, TimeProvider.System));
    }

    [Fact]
    public void FormatterRejectsUnsupportedFormat()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FilesLogEntryFormatter.Format(TestEntries.Create(), (FilesLogFormat)99));

        Assert.Equal("format", exception.ParamName);
        Assert.Equal((FilesLogFormat)99, exception.ActualValue);
    }
}
