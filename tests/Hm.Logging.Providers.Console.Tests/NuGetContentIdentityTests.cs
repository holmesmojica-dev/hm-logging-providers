using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hm.Logging.Providers.ReleaseTools;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using Xunit;

namespace Hm.Logging.Providers.Console.Tests;

public sealed class NuGetContentIdentityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hm-provider-content-identity-{Guid.NewGuid():N}");

    public NuGetContentIdentityTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void EquivalentUnsignedPackagesHaveTheSameContentIdentity()
    {
        string local = CreatePackage("local.nupkg", [1, 2, 3]);
        string remote = Path.Combine(_directory, "remote.nupkg");
        File.Copy(local, remote);

        NuGetContentIdentity.AssertEquivalent(local, remote);
    }

    [Fact]
    public void MatchingMetadataWithDifferentPayloadHasDifferentContentIdentity()
    {
        string local = CreatePackage("local.nupkg", [1, 2, 3]);
        string remote = CreatePackage("remote.nupkg", [3, 2, 1]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            NuGetContentIdentity.AssertEquivalent(local, remote));

        Assert.Contains("different content identities", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPackageFailsClosed()
    {
        string local = CreatePackage("local.nupkg", [1, 2, 3]);
        string remote = Path.Combine(_directory, "remote.nupkg");
        File.WriteAllBytes(remote, [1, 2, 3]);

        _ = Assert.ThrowsAny<Exception>(() => NuGetContentIdentity.AssertEquivalent(local, remote));
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("AQID")]
    public void InvalidContentHashFailsClosed(string contentHash)
    {
        _ = Assert.Throws<InvalidDataException>(() => NuGetContentIdentity.ParseContentHash(contentHash));
    }

    [Fact]
    public void CommandRequiresTheExactOperationAndArguments()
    {
        Assert.Equal(2, Program.Main([]));
        Assert.Equal(2, Program.Main(["unknown", "local.nupkg", "remote.nupkg"]));
    }

    [Fact]
    public void CommandReturnsSuccessOnlyForEquivalentPackages()
    {
        string local = CreatePackage("local.nupkg", [1, 2, 3]);
        string equivalent = Path.Combine(_directory, "equivalent.nupkg");
        File.Copy(local, equivalent);
        string different = CreatePackage("different.nupkg", [3, 2, 1]);

        Assert.Equal(0, Program.Main(["assert-content-identity", local, equivalent]));
        Assert.Equal(1, Program.Main(["assert-content-identity", local, different]));
    }

    [Fact]
    public async Task RepositorySignedPackageMatchesItsUnsignedContentIdentity()
    {
        string local = CreatePackage("local.nupkg", [1, 2, 3]);
        string remote = Path.Combine(_directory, "remote.nupkg");
        await RepositorySignAsync(local, remote);

        using (var signedPackage = new PackageArchiveReader(remote))
        {
            Assert.True(await signedPackage.IsSignedAsync(TestContext.Current.CancellationToken));
        }

        NuGetContentIdentity.AssertEquivalent(local, remote);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string CreatePackage(string name, byte[] assemblyBytes)
    {
        string path = Path.Combine(_directory, name);
        using ZipArchive package = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(package, "HDev.Hm.Logging.Providers.Console.nuspec", "<package><metadata><id>HDev.Hm.Logging.Providers.Console</id><version>8.7.6</version><repository commit='1234567890123456789012345678901234567890'/></metadata></package>"u8.ToArray());
        WriteEntry(package, "lib/net10.0/Hm.Logging.Providers.Console.dll", assemblyBytes);
        return path;
    }

    private static void WriteEntry(ZipArchive package, string name, byte[] bytes)
    {
        using Stream stream = package.CreateEntry(name).Open();
        stream.Write(bytes);
    }

    private static async Task RepositorySignAsync(string source, string destination)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=HM Provider Content Identity Test", key, System.Security.Cryptography.HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3")], false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var input = new Lazy<Stream>(() => File.OpenRead(source));
        var output = new Lazy<Stream>(() => File.Open(destination, FileMode.Create, FileAccess.ReadWrite, FileShare.None));
        try
        {
            var options = new SigningOptions(input, output, overwrite: false, new X509SignatureProvider(new PassthroughTimestampProvider()), NullLogger.Instance);
            var signRequest = new RepositorySignPackageRequest(
                certificate,
                NuGet.Common.HashAlgorithmName.SHA256,
                NuGet.Common.HashAlgorithmName.SHA256,
                new Uri("https://api.nuget.org/v3/index.json"),
                ["hm-provider-test"]);
            await SigningUtility.SignAsync(options, signRequest, TestContext.Current.CancellationToken);
        }
        finally
        {
            if (input.IsValueCreated) { input.Value.Dispose(); }
            if (output.IsValueCreated) { output.Value.Dispose(); }
        }
    }

    private sealed class PassthroughTimestampProvider : ITimestampProvider
    {
        public Task<PrimarySignature> TimestampSignatureAsync(
            PrimarySignature primarySignature,
            TimestampRequest request,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(primarySignature);
        }
    }
}
