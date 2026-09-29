using Hm.Logging.Providers.Files.Configuration;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class FileSizeTests
{
    [Fact]
    public void FactoriesUseDecimalSiUnitsAndPreserveFractionalUnits()
    {
        Assert.Equal(1UL, FileSize.FromBytes(1).Bytes);
        Assert.Equal(1_500UL, FileSize.FromKB(1.5m).Bytes);
        Assert.Equal(1_500_000UL, FileSize.FromMB(1.5m).Bytes);
        Assert.Equal(1_500_000_000UL, FileSize.FromGB(1.5m).Bytes);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("kilobytes")]
    [InlineData("megabytes")]
    [InlineData("gigabytes")]
    public void FactoriesRejectNonPositiveValues(string factory)
    {
        _ = factory switch
        {
            "bytes" => Assert.Throws<ArgumentOutOfRangeException>(() => FileSize.FromBytes(0)),
            "kilobytes" => Assert.Throws<ArgumentOutOfRangeException>(() => FileSize.FromKB(0)),
            "megabytes" => Assert.Throws<ArgumentOutOfRangeException>(() => FileSize.FromMB(-1)),
            _ => Assert.Throws<ArgumentOutOfRangeException>(() => FileSize.FromGB(0))
        };
    }

    [Fact]
    public void DecimalFactoriesRejectFractionalBytesAndOverflow()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => FileSize.FromKB(0.0001m));
        _ = Assert.Throws<OverflowException>(() => FileSize.FromGB(decimal.MaxValue));
    }

    [Fact]
    public void OptionsExposeApprovedDefaults()
    {
        var options = new FilesProviderOptions();

        Assert.Equal("logs", options.DirectoryPath);
        Assert.False(options.GroupBySource);
        Assert.Equal(30U, options.RetentionDays);
        Assert.Equal(100_000_000UL, options.MaximumFileSize!.Value.Bytes);
        Assert.Null(options.MaximumTotalSize);
        Assert.Equal(FilesLogFormat.Json, options.Format);
    }

    [Fact]
    public void SettingsNormalizeBlankDirectoryAndRejectInvalidValues()
    {
        var settings = FilesProviderSettings.FromOptions(new FilesProviderOptions { DirectoryPath = " " });
        Assert.Equal("logs", Path.GetFileName(settings.DirectoryPath));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FilesProviderSettings.FromOptions(new FilesProviderOptions { Format = (FilesLogFormat)99 }));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FilesProviderSettings.FromOptions(new FilesProviderOptions { MaximumFileSize = default(FileSize) }));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FilesProviderSettings.FromOptions(new FilesProviderOptions { MaximumTotalSize = default(FileSize) }));
    }
}
