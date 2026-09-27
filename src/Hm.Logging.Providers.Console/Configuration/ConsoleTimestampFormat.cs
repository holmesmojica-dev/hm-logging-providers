namespace Hm.Logging.Providers.Console.Configuration;

/// <summary>
/// Specifies how the main timestamp is represented in text output.
/// </summary>
public enum ConsoleTimestampFormat
{
    /// <summary>
    /// UTC ISO 8601 round-trip format.
    /// </summary>
    Iso8601 = 0,

    /// <summary>
    /// Date and time in <c>yyyy-MM-dd HH:mm:ss</c> format.
    /// </summary>
    DateTime = 1,

    /// <summary>
    /// Time in <c>HH:mm:ss.fff</c> format.
    /// </summary>
    Time = 2
}
