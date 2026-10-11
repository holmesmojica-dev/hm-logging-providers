namespace Hm.Logging.Providers.Shared.Identity;

internal static class ProviderIdentity
{
    internal static Guid Resolve(Guid? providerId)
    {
        return providerId switch
        {
            null => Guid.NewGuid(),
            Guid value when value != Guid.Empty => value,
            _ => throw new ArgumentOutOfRangeException(
                nameof(providerId),
                providerId,
                "The provider identifier must be a nonempty GUID when supplied.")
        };
    }
}
