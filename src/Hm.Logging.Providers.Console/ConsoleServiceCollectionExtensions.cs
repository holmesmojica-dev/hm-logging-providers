using Hm.Logging.Abstractions;
using Hm.Logging.Providers.Console;
using Hm.Logging.Providers.Console.Configuration;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // DI extensions intentionally share Core's Hm.Logging.Extensions namespace.
namespace Hm.Logging.Extensions;
#pragma warning restore IDE0130

/// <summary>
/// Provides dependency-injection registration for the HM Logging provider that targets process standard streams.
/// </summary>
public static class ConsoleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Console provider as a singleton <see cref="ILogProvider"/>.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">
    /// An optional delegate that configures the provider. When omitted, text output, colors, DateTime timestamps,
    /// multiline exceptions, and default severity-based stream routing are used.
    /// </param>
    /// <returns>The same service collection so additional registrations can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This method registers only the Console destination. It does not register or replace HM Logging Core;
    /// call <see cref="ServiceCollectionExtensions.AddHmLogging(IServiceCollection, Action{Hm.Logging.Configuration.LoggingOptions}?)"/>
    /// separately. The configured values are validated and captured during registration for the singleton lifetime;
    /// Console V1 does not hot-reload options.
    /// </para>
    /// <para>
    /// Multiple HM Logging providers may be registered in the same service collection. Core dispatches normalized
    /// entries to every registered <see cref="ILogProvider"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Register Core and then add Console with its defaults:
    /// <code>
    /// services
    ///     .AddHmLogging()
    ///     .AddLoggingConsole();
    /// </code>
    /// Configure JSON output when needed:
    /// <code>
    /// services.AddLoggingConsole(options =&gt;
    /// {
    ///     options.Format = ConsoleOutputFormat.Json;
    ///     options.UseStandardErrorForErrors = true;
    /// });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="configure"/> assigns an unsupported enum value.
    /// </exception>
    /// <seealso cref="ConsoleProviderOptions"/>
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
