namespace Hm.Logging.Providers.Files.Configuration;

/// <summary>
/// Specifies how the Files provider serializes each normalized log entry.
/// </summary>
/// <remarks>Both formats are written as UTF-8 without a byte-order mark.</remarks>
public enum FilesLogFormat
{
    /// <summary>
    /// Writes one complete structured JSON object followed by a line feed for each entry, using the
    /// <c>.jsonl</c> extension. This is the default format.
    /// </summary>
    Json = 0,

    /// <summary>
    /// Writes a human-readable representation, terminated by a line feed, using the <c>.log</c> extension.
    /// </summary>
    Text = 1
}
