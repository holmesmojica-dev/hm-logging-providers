using System.Reflection;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void FilesV1ExportsOnlyItsApprovedPublicSurface()
    {
        Assembly assembly = typeof(FilesProvider).Assembly;

        Assert.Equal(
            [
                "Hm.Logging.Extensions.FilesServiceCollectionExtensions",
                "Hm.Logging.Providers.Files.Configuration.FileSize",
                "Hm.Logging.Providers.Files.Configuration.FilesLogFormat",
                "Hm.Logging.Providers.Files.Configuration.FilesProviderOptions",
                "Hm.Logging.Providers.Files.FilesProvider"
            ],
            assembly.ExportedTypes.Select(type => type.FullName).Order(StringComparer.Ordinal).ToArray());
    }
}
