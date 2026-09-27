namespace Hm.Logging.Providers.Console.Configuration;

/// <summary>
/// Specifies how the normalized main timestamp is represented in text output.
/// </summary>
/// <remarks>JSON timestamps and temporal metadata values are not affected by this setting.</remarks>
public enum ConsoleTimestampFormat
{
    /// <summary>
    /// UTC ISO 8601 round-trip format, using the <c>O</c> format specifier.
    /// </summary>
    Iso8601 = 0,

    /// <summary>
    /// UTC date and time in <c>yyyy-MM-dd HH:mm:ss</c> format.
    /// </summary>
    DateTime = 1,

    /// <summary>
    /// UTC time in <c>HH:mm:ss.fff</c> format.
    /// </summary>
    Time = 2
}
