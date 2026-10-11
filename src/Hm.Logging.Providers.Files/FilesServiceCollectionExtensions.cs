using Hm.Logging.Abstractions;
using Hm.Logging.Providers.Files;
using Hm.Logging.Providers.Files.Configuration;
using Hm.Logging.Providers.Shared.Identity;
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
    /// <param name="providerId">
    /// Optional instance identifier used for selective routing. When omitted, the singleton receives a generated
    /// nonempty identifier that is not stable across application lifecycles.
    /// </param>
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
    /// Thrown when <paramref name="providerId"/> is <see cref="Guid.Empty"/>, the configured format is unsupported,
    /// or a file-size limit is zero.
    /// </exception>
    public static IServiceCollection AddLoggingFiles(
        this IServiceCollection services,
        Guid? providerId = null,
        Action<FilesProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        Guid? validatedProviderId = providerId is null ? null : ProviderIdentity.Resolve(providerId);

        var options = new FilesProviderOptions();
        configure?.Invoke(options);
        var settings = FilesProviderSettings.FromOptions(options);
        _ = services.AddSingleton<ILogProvider>(
            _ => new FilesProvider(ProviderIdentity.Resolve(validatedProviderId), settings, TimeProvider.System));
        return services;
    }
}
