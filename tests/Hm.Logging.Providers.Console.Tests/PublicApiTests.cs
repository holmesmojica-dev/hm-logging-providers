using System.Reflection;
using Xunit;

namespace Hm.Logging.Providers.Console.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void ConsoleV1ExportsOnlyItsApprovedPublicSurface()
    {
        Assembly assembly = typeof(ConsoleProvider).Assembly;

        Assert.Equal(
            [
                "Hm.Logging.Extensions.ConsoleServiceCollectionExtensions",
                "Hm.Logging.Providers.Console.Configuration.ConsoleExceptionFormat",
                "Hm.Logging.Providers.Console.Configuration.ConsoleOutputFormat",
                "Hm.Logging.Providers.Console.Configuration.ConsoleProviderOptions",
                "Hm.Logging.Providers.Console.Configuration.ConsoleTimestampFormat",
                "Hm.Logging.Providers.Console.ConsoleProvider"
            ],
            assembly.ExportedTypes.Select(type => type.FullName).Order(StringComparer.Ordinal).ToArray());
    }
}
