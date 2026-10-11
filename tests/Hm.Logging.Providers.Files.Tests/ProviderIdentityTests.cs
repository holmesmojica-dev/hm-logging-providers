using Hm.Logging.Providers.Shared.Identity;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class ProviderIdentityTests
{
    [Fact]
    public void ResolveGeneratesDistinctNonemptyIdentifiersWhenOmitted()
    {
        Guid first = ProviderIdentity.Resolve(null);
        Guid second = ProviderIdentity.Resolve(null);

        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(Guid.Empty, second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ResolvePreservesExplicitIdentifier()
    {
        var providerId = Guid.NewGuid();

        Guid result = ProviderIdentity.Resolve(providerId);

        Assert.Equal(providerId, result);
    }

    [Fact]
    public void ResolveRejectsExplicitEmptyIdentifier()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ProviderIdentity.Resolve(Guid.Empty));

        Assert.Equal("providerId", exception.ParamName);
        Assert.Equal(Guid.Empty, exception.ActualValue);
    }
}
