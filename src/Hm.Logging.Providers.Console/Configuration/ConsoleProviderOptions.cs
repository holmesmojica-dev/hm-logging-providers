namespace Hm.Logging.Providers.Console.Configuration;

/// <summary>
/// Configures how the HM Logging Console provider presents and routes normalized log entries.
/// </summary>
/// <remarks>
/// The provider captures these values when it is constructed or registered. Console V1 does not hot-reload
/// configuration. Use <see cref="Extensions.ConsoleServiceCollectionExtensions.AddLoggingConsole(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{ConsoleProviderOptions}?)"/>
/// to configure the singleton provider during service registration.
/// </remarks>
/// <example>
/// <code>
/// services.AddLoggingConsole(options =&gt;
/// {
///     options.Format = ConsoleOutputFormat.Json;
///     options.UseStandardErrorForErrors = true;
/// });
/// </code>
/// </example>
public sealed class ConsoleProviderOptions
{
    /// <summary>
    /// Gets or sets whether each entry is emitted as human-readable text or structured JSON.
    /// </summary>
    /// <value>The default is <see cref="ConsoleOutputFormat.Text"/>.</value>
    public ConsoleOutputFormat Format { get; set; } = ConsoleOutputFormat.Text;

    /// <summary>
    /// Gets or sets the main timestamp presentation used by text output.
    /// </summary>
    /// <value>The default is <see cref="ConsoleTimestampFormat.DateTime"/>.</value>
    /// <remarks>
    /// This option does not affect JSON, whose main timestamp always uses a standardized ISO 8601 representation,
    /// or temporal values in metadata.
    /// </remarks>
    public ConsoleTimestampFormat TimestampFormat { get; set; } = ConsoleTimestampFormat.DateTime;

    /// <summary>
    /// Gets or sets a value indicating whether text output includes ANSI level colors.
    /// </summary>
    /// <value>The default is <see langword="true"/>.</value>
    /// <remarks>JSON never includes ANSI color sequences, regardless of this value.</remarks>
    public bool UseColors { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether high-severity entries are routed to standard error.
    /// </summary>
    /// <value>The default is <see langword="true"/>.</value>
    /// <remarks>
    /// When enabled, Error and Critical entries go to standard error while Trace, Debug, Information, and Warning
    /// go to standard output. When disabled, every level goes to standard output.
    /// </remarks>
    public bool UseStandardErrorForErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets how opaque exception text is presented in text output.
    /// </summary>
    /// <value>The default is <see cref="ConsoleExceptionFormat.Multiline"/>.</value>
    /// <remarks>
    /// This option does not affect JSON, which preserves the semantic exception text and its line breaks.
    /// </remarks>
    public ConsoleExceptionFormat ExceptionFormat { get; set; } = ConsoleExceptionFormat.Multiline;
}
