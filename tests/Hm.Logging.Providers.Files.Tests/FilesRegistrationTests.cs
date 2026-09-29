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
            .AddLoggingFiles(options => options.DirectoryPath = directory.Path);

        Assert.Same(services, returned);
        using ServiceProvider provider = services.BuildServiceProvider();
        ILogProvider first = provider.GetRequiredService<ILogProvider>();
        ILogProvider second = provider.GetRequiredService<ILogProvider>();
        _ = Assert.IsType<FilesProvider>(first);
        Assert.Same(first, second);
        Assert.NotNull(provider.GetRequiredService<ILoggerService>());
    }

    [Fact]
    public void RegistrationValidatesAndSnapshotsConfigurationImmediately()
    {
        var services = new ServiceCollection();
        FilesProviderOptions? captured = null;
        _ = services.AddLoggingFiles(options =>
        {
            captured = options;
            options.Format = FilesLogFormat.Text;
        });
        captured!.Format = (FilesLogFormat)99;

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = Assert.IsType<FilesProvider>(provider.GetRequiredService<ILogProvider>());

        var invalidServices = new ServiceCollection();
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            invalidServices.AddLoggingFiles(options => options.Format = (FilesLogFormat)99));
        Assert.Empty(invalidServices);
    }
}
