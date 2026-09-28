namespace Hm.Logging.Providers.Console.Configuration;

/// <summary>
/// Specifies the destination representation emitted for each Console log entry.
/// </summary>
public enum ConsoleOutputFormat
{
    /// <summary>
    /// Human-readable, optionally colored text intended for terminals and captured standard streams.
    /// </summary>
    Text = 0,

    /// <summary>
    /// Structured JSON with semantic level names, ISO 8601 timestamps, and no ANSI color sequences.
    /// </summary>
    Json = 1
}
