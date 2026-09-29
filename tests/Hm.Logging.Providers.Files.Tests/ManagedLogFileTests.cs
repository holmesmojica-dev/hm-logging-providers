using Hm.Logging.Providers.Files.Configuration;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class ManagedLogFileTests
{
    [Fact]
    public void DiscoverReturnsEmptyForNonexistentRoot()
    {
        using var directory = new TemporaryDirectory();

        IReadOnlyList<ManagedLogFile> files = ManagedLogFile.Discover(directory.Path);

        Assert.Empty(files);
    }

    [Theory]
    [InlineData("logs-2026-09-28.1.jsonl", true, 1)]
    [InlineData("logs-2026-09-28.2147483647.log", true, int.MaxValue)]
    [InlineData("logs-2026-09-28.2147483648.jsonl", false, 0)]
    [InlineData("logs-2026-09-28.invalid.jsonl", false, 0)]
    [InlineData("notes.txt", false, 0)]
    public async Task TryParseRejectsMalformedOrUnsupportedSegmentsWithoutThrowing(
        string fileName,
        bool expectedResult,
        int expectedSegment)
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string path = Path.Combine(directory.Path, fileName);
        await File.WriteAllTextAsync(path, "content", TestContext.Current.CancellationToken);

        bool result = ManagedLogFile.TryParse(directory.Path, path, out ManagedLogFile? file);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.NotNull(file);
            Assert.Equal(expectedSegment, file.Segment);
        }
        else
        {
            Assert.Null(file);
        }
    }

    [Fact]
    public async Task MaintenanceLeavesOverflowMalformedAndOrdinaryUnrecognizedFilesUntouched()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(directory.Path);
        string overflow = Path.Combine(directory.Path, "logs-2020-01-01.999999999999999999999.jsonl");
        string malformed = Path.Combine(directory.Path, "logs-2020-01-01.invalid.jsonl");
        string ordinary = Path.Combine(directory.Path, "notes.txt");
        string recognized = Path.Combine(directory.Path, "logs-2020-01-01.1.jsonl");
        foreach (string path in new[] { overflow, malformed, ordinary, recognized })
        {
            await File.WriteAllTextAsync(path, "content", TestContext.Current.CancellationToken);
        }

        using var provider = new FilesProvider(
            new FilesProviderOptions
            {
                DirectoryPath = directory.Path,
                RetentionDays = 0
            },
            new TestTimeProvider(new DateTimeOffset(2026, 9, 28, 10, 30, 0, TimeSpan.Zero)));

        await provider.WriteAsync(TestEntries.Create(), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(overflow));
        Assert.True(File.Exists(malformed));
        Assert.True(File.Exists(ordinary));
        Assert.False(File.Exists(recognized));
    }

    [Theory]
    [InlineData("payments-2026-09-28.jsonl")]
    [InlineData("actual/other-2026-09-28.jsonl")]
    [InlineData("outer/inner/inner-2026-09-28.jsonl")]
    public async Task TryParseRejectsProviderLookingFilesOutsideApprovedLayout(string relativePath)
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "content", TestContext.Current.CancellationToken);

        bool result = ManagedLogFile.TryParse(directory.Path, path, out ManagedLogFile? file);

        Assert.False(result);
        Assert.Null(file);
    }
}
