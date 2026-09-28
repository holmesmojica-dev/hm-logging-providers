using Hm.Logging.Abstractions;
using Hm.Logging.Extensions;
using Hm.Logging.Providers.Console.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hm.Logging.Providers.Console.Tests;

public sealed class ConsoleRegistrationTests
{
    [Fact]
    public void RegistrationIsChainableAndAddsSingletonProviderWithoutReplacingCore()
    {
        var services = new ServiceCollection();

        IServiceCollection returned = services
            .AddHmLogging()
            .AddLoggingConsole(options => options.UseColors = false);

        Assert.Same(services, returned);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ILogProvider first = serviceProvider.GetRequiredService<ILogProvider>();
        ILogProvider second = serviceProvider.GetRequiredService<ILogProvider>();
        _ = Assert.IsType<ConsoleProvider>(first);
        Assert.Same(first, second);
        Assert.NotNull(serviceProvider.GetRequiredService<ILoggerService>());
    }

    [Fact]
    public void RegistrationRejectsInvalidConfigurationImmediately()
    {
        var services = new ServiceCollection();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => services.AddLoggingConsole(options => options.Format = (ConsoleOutputFormat)99));

        Assert.Contains("Format", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void RegistrationSnapshotsConfigurationImmediately()
    {
        var services = new ServiceCollection();
        ConsoleProviderOptions? capturedOptions = null;
        _ = services.AddLoggingConsole(options =>
        {
            options.Format = ConsoleOutputFormat.Text;
            capturedOptions = options;
        });
        capturedOptions!.Format = (ConsoleOutputFormat)99;

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        _ = Assert.IsType<ConsoleProvider>(serviceProvider.GetRequiredService<ILogProvider>());
    }
}
