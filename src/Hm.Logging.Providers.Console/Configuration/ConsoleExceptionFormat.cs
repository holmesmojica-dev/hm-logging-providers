namespace Hm.Logging.Providers.Console.Configuration;

/// <summary>
/// Specifies how exception text is represented in text output.
/// </summary>
public enum ConsoleExceptionFormat
{
    /// <summary>
    /// Preserves the exception's original line structure.
    /// </summary>
    Multiline = 0,

    /// <summary>
    /// Replaces exception line breaks with <c> | </c> separators.
    /// </summary>
    Compact = 1
}
