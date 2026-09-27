using Hm.Logging.Abstractions;
using Hm.Logging.Providers.Console;
using Hm.Logging.Providers.Console.Configuration;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // DI extensions intentionally share Core's Hm.Logging.Extensions namespace.
namespace Hm.Logging.Extensions;
#pragma warning restore IDE0130

/// <summary>
/// Provides dependency-injection registration for the HM Logging Console provider.
/// </summary>
public static class ConsoleServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Console provider to the HM Logging provider collection.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">An optional delegate that configures the Console provider.</param>
    /// <returns>The same service collection so additional registrations can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an enum option has an unsupported value.</exception>
    public static IServiceCollection AddLoggingConsole(
        this IServiceCollection services,
        Action<ConsoleProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ConsoleProviderOptions();
        configure?.Invoke(options);
        var settings = ConsoleProviderSettings.FromOptions(options);

        _ = services.AddSingleton<ILogProvider>(_ => new ConsoleProvider(settings, new SystemConsoleWriter()));
        return services;
    }
}
