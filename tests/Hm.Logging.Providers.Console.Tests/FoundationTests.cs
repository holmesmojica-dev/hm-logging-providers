using System.Reflection;
using Xunit;

namespace Hm.Logging.Providers.Console.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void FoundationAssemblyDoesNotExposeSpeculativePublicApi()
    {
        var assembly = Assembly.Load("Hm.Logging.Providers.Console");

        Assert.Empty(assembly.ExportedTypes);
    }
}
