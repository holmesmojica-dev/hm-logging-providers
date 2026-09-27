namespace Hm.Logging.Providers.Console.Configuration;

/// <summary>
/// Configures the HM Logging Console provider.
/// </summary>
public sealed class ConsoleProviderOptions
{
    /// <summary>
    /// Gets or sets the output representation. The default is <see cref="ConsoleOutputFormat.Text"/>.
    /// </summary>
    public ConsoleOutputFormat Format { get; set; } = ConsoleOutputFormat.Text;

    /// <summary>
    /// Gets or sets the main timestamp representation for text output. The default is
    /// <see cref="ConsoleTimestampFormat.DateTime"/>.
    /// </summary>
    public ConsoleTimestampFormat TimestampFormat { get; set; } = ConsoleTimestampFormat.DateTime;

    /// <summary>
    /// Gets or sets a value indicating whether text output uses ANSI colors. The default is <see langword="true"/>.
    /// </summary>
    public bool UseColors { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Error and Critical entries are written to standard error.
    /// The default is <see langword="true"/>.
    /// </summary>
    public bool UseStandardErrorForErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets the exception representation for text output. The default is
    /// <see cref="ConsoleExceptionFormat.Multiline"/>.
    /// </summary>
    public ConsoleExceptionFormat ExceptionFormat { get; set; } = ConsoleExceptionFormat.Multiline;
}
