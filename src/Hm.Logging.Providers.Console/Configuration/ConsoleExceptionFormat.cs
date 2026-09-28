namespace Hm.Logging.Providers.Console.Configuration;

/// <summary>
/// Specifies how opaque exception text is represented in text output.
/// </summary>
/// <remarks>JSON preserves exception text independently of this terminal-oriented setting.</remarks>
public enum ConsoleExceptionFormat
{
    /// <summary>
    /// Preserves the exception text's original line structure for developer-readable output.
    /// </summary>
    Multiline = 0,

    /// <summary>
    /// Produces one-line-oriented output by replacing each exception line break with a <c> | </c> separator.
    /// </summary>
    Compact = 1
}
