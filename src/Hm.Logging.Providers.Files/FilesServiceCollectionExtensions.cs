using Hm.Logging.Abstractions;
using Hm.Logging.Providers.Files;
using Hm.Logging.Providers.Files.Configuration;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // DI extensions intentionally share Core's Hm.Logging.Extensions namespace.
namespace Hm.Logging.Extensions;
#pragma warning restore IDE0130

/// <summary>
/// Provides dependency-injection registration for the HM Logging Files destination.
/// </summary>
public static class FilesServiceCollectionExtensions
{
    /// <summary>
    /// Registers one Files provider as a singleton <see cref="ILogProvider"/>.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">
    /// An optional delegate that configures storage, formatting, rotation, retention, and capacity. When omitted,
    /// the defaults from <see cref="FilesProviderOptions"/> are used.
    /// </param>
    /// <returns>The same service collection so additional registrations can be chained.</returns>
    /// <remarks>
    /// This method registers only the Files destination. It does not register or replace HM Logging Core.
    /// Configuration is validated and captured during this call; later changes to the configured options are not
    /// observed. Register HM Logging Core separately, then chain this method during service configuration.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the configured format is unsupported or a file-size limit is zero.
    /// </exception>
    public static IServiceCollection AddLoggingFiles(
        this IServiceCollection services,
        Action<FilesProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FilesProviderOptions();
        configure?.Invoke(options);
        var settings = FilesProviderSettings.FromOptions(options);
        _ = services.AddSingleton<ILogProvider>(_ => new FilesProvider(settings, TimeProvider.System));
        return services;
    }
}
