using Hm.Logging.Abstractions;
using Hm.Logging.Extensions;
using Hm.Logging.Providers.Files.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hm.Logging.Providers.Files.Tests;

public sealed class FilesRegistrationTests
{
    [Fact]
    public void RegistrationIsChainableAndAddsSingletonWithoutReplacingCore()
    {
        using var directory = new TemporaryDirectory();
        var services = new ServiceCollection();

        IServiceCollection returned = services
            .AddHmLogging()
            .AddLoggingFiles(configure: options => options.DirectoryPath = directory.Path);

        Assert.Same(services, returned);
        using ServiceProvider provider = services.BuildServiceProvider();
        ILogProvider first = provider.GetRequiredService<ILogProvider>();
        ILogProvider second = provider.GetRequiredService<ILogProvider>();
        FilesProvider filesProvider = Assert.IsType<FilesProvider>(first);
        Assert.Same(first, second);
        Assert.NotEqual(Guid.Empty, filesProvider.ProviderId);
        Assert.NotNull(provider.GetRequiredService<ILoggerService>());
    }

    [Fact]
    public void RegistrationValidatesAndSnapshotsConfigurationImmediately()
    {
        var services = new ServiceCollection();
        FilesProviderOptions? captured = null;
        _ = services.AddLoggingFiles(configure: options =>
        {
            captured = options;
            options.Format = FilesLogFormat.Text;
        });
        captured!.Format = (FilesLogFormat)99;

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = Assert.IsType<FilesProvider>(provider.GetRequiredService<ILogProvider>());

        var invalidServices = new ServiceCollection();
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            invalidServices.AddLoggingFiles(configure: options => options.Format = (FilesLogFormat)99));
        Assert.Empty(invalidServices);
    }

    [Fact]
    public async Task RegistrationUsesExplicitProviderIdentifierAndFunctionalOptions()
    {
        using var directory = new TemporaryDirectory();
        var providerId = Guid.NewGuid();
        var services = new ServiceCollection();
        _ = services.AddLoggingFiles(
            providerId: providerId,
            configure: options =>
            {
                options.DirectoryPath = directory.Path;
                options.Format = FilesLogFormat.Clef;
            });

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        FilesProvider provider = Assert.IsType<FilesProvider>(serviceProvider.GetRequiredService<ILogProvider>());
        await provider.WriteAsync(TestEntries.Create("registered identity"), TestContext.Current.CancellationToken);

        Assert.Equal(providerId, provider.ProviderId);
        _ = Assert.Single(Directory.GetFiles(directory.Path, "*.clef"));
    }

    [Fact]
    public void RegistrationRejectsExplicitEmptyProviderIdentifier()
    {
        var services = new ServiceCollection();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => services.AddLoggingFiles(providerId: Guid.Empty));

        Assert.Equal("providerId", exception.ParamName);
        Assert.Empty(services);
    }
}
