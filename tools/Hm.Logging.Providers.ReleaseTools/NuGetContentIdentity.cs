using System.Security.Cryptography;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace Hm.Logging.Providers.ReleaseTools;

public static class NuGetContentIdentity
{
    private const int Sha512Length = 64;

    public static void AssertEquivalent(string localPackagePath, string remotePackagePath)
    {
        byte[] localHash = GetContentHash(localPackagePath);
        byte[] remoteHash = GetContentHash(remotePackagePath);

        if (!CryptographicOperations.FixedTimeEquals(localHash, remoteHash))
        {
            throw new InvalidDataException("The NuGet packages have different content identities.");
        }
    }

    public static byte[] GetContentHash(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        using var package = new PackageArchiveReader(packagePath);
        CancellationToken cancellationToken = CancellationToken.None;
        if (package.IsSignedAsync(cancellationToken).GetAwaiter().GetResult())
        {
            PrimarySignature signature = package.GetPrimarySignatureAsync(cancellationToken).GetAwaiter().GetResult()
                ?? throw new InvalidDataException("The signed NuGet package has no primary signature.");
            package.ValidateIntegrityAsync(signature.SignatureContent, cancellationToken).GetAwaiter().GetResult();
        }

        return ParseContentHash(package.GetContentHash(cancellationToken));
    }

    internal static byte[] ParseContentHash(string contentHash)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentHash);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The NuGet package content hash is not valid Base64.", exception);
        }

        return bytes.Length == Sha512Length && string.Equals(Convert.ToBase64String(bytes), contentHash, StringComparison.Ordinal)
            ? bytes
            : throw new InvalidDataException("The NuGet package content hash is not a canonical SHA-512 value.");
    }
}
