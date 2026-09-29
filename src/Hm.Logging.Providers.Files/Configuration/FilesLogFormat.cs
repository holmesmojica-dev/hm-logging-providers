namespace Hm.Logging.Providers.Files.Configuration;

/// <summary>
/// Specifies the representation written by the Files provider.
/// </summary>
public enum FilesLogFormat
{
    /// <summary>
    /// Writes one structured JSON object per line using the <c>.jsonl</c> extension.
    /// </summary>
    Json = 0,

    /// <summary>
    /// Writes a human-readable representation using the <c>.log</c> extension.
    /// </summary>
    Text = 1
}
