namespace Hm.Logging.Providers.Files.Configuration;

/// <summary>
/// Configures storage, formatting, rotation, and retention for the HM Logging Files provider.
/// </summary>
/// <remarks>
/// Values are validated and captured for the singleton provider lifetime. Files V1 does not hot-reload configuration.
/// </remarks>
public sealed class FilesProviderOptions
{
    /// <summary>
    /// Gets or sets the managed root directory. Null, empty, or whitespace values normalize to <c>logs</c>.
    /// </summary>
    public string DirectoryPath { get; set; } = "logs";

    /// <summary>
    /// Gets or sets whether physical files are partitioned by a safe deterministic representation of Source.
    /// </summary>
    public bool GroupBySource { get; set; }

    /// <summary>
    /// Gets or sets the number of complete logical UTC dates retained, or <see langword="null"/> to disable age cleanup.
    /// </summary>
    public uint? RetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the strict maximum size of each physical file, or <see langword="null"/> to disable segmentation.
    /// </summary>
    public FileSize? MaximumFileSize { get; set; } = FileSize.FromMB(100);

    /// <summary>
    /// Gets or sets the optional capacity of all recognized Files-managed logs under the configured root.
    /// </summary>
    public FileSize? MaximumTotalSize { get; set; }

    /// <summary>
    /// Gets or sets the destination representation. The default is JSON Lines.
    /// </summary>
    public FilesLogFormat Format { get; set; } = FilesLogFormat.Json;
}
