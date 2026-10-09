namespace Hm.Logging.Providers.Files.Configuration;

/// <summary>
/// Specifies how the Files provider serializes each normalized log entry.
/// </summary>
/// <remarks>
/// Every format is written as UTF-8 without a byte-order mark. JSON-based formats contain one complete object
/// per line, while <see cref="Text"/> is intended for direct human reading.
/// </remarks>
public enum FilesLogFormat
{
    /// <summary>
    /// Writes each normalized HM Logging entry as one complete structured JSON object followed by a line feed,
    /// using the <c>.jsonl</c> extension. This is the default format.
    /// </summary>
    Json = 0,

    /// <summary>
    /// Writes each normalized entry as a human-readable representation terminated by a line feed, using the
    /// <c>.log</c> extension. Use this format when people, rather than structured-log consumers, primarily inspect
    /// the files.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Writes each normalized entry as one Compact Log Event Format (CLEF) JSON object followed by a line feed,
    /// using the <c>.clef</c> extension. Use this open structured format with CLEF-aware consumers such as Seq.
    /// </summary>
    Clef = 2
}
