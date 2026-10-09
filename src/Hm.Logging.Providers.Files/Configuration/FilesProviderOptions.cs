namespace Hm.Logging.Providers.Files.Configuration;

/// <summary>
/// Configures where and how the HM Logging Files provider stores normalized log entries.
/// </summary>
/// <remarks>
/// Values are validated and captured when the singleton provider is registered or constructed. Changes made to this
/// options instance afterward are not observed by the provider.
/// </remarks>
public sealed class FilesProviderOptions
{
    /// <summary>
    /// Gets or sets the provider-managed root directory. The default is <c>logs</c>.
    /// </summary>
    /// <remarks>
    /// Null, empty, or whitespace-only values normalize to <c>logs</c>. Relative paths are resolved to full paths
    /// when configuration is captured.
    /// </remarks>
    public string DirectoryPath { get; set; } = "logs";

    /// <summary>
    /// Gets or sets whether entries are partitioned into source-specific directories and files. The default is
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// When enabled, each Source is converted to a deterministic filesystem-safe name. A missing, empty, or
    /// whitespace-only Source uses <c>unknown</c>. When disabled, all Sources share the <c>logs</c> file stream.
    /// </remarks>
    public bool GroupBySource { get; set; }

    /// <summary>
    /// Gets or sets the number of complete logical UTC dates retained. The default is 30; <see langword="null"/>
    /// disables age-based cleanup.
    /// </summary>
    /// <remarks>
    /// Maintenance runs lazily during writes. It deletes only recognized Files-managed logs from expired UTC dates
    /// and never deletes the current or a future UTC date. A value of zero removes all recognized prior-date logs.
    /// </remarks>
    public uint? RetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the strict maximum size of each physical log file. The default is 100 decimal SI MB;
    /// <see langword="null"/> disables size-based segmentation.
    /// </summary>
    /// <remarks>
    /// The limit includes the complete serialized entry and its terminating line feed. An entry that cannot fit in
    /// an empty file is rejected. Otherwise, an entry that cannot fit in the current file starts the next numbered
    /// segment.
    /// </remarks>
    public FileSize? MaximumFileSize { get; set; } = FileSize.FromMB(100);

    /// <summary>
    /// Gets or sets the total capacity allowed for recognized Files-managed logs under the configured root. The
    /// default is <see langword="null"/>, which disables total-capacity enforcement.
    /// </summary>
    /// <remarks>
    /// Only files matching the provider's approved root or source-partitioned naming layouts count toward the limit;
    /// unrelated files are ignored. To admit a write, older UTC dates may be removed oldest-first, but files from the
    /// current UTC date are never removed for capacity. The write is rejected when it still cannot fit.
    /// </remarks>
    public FileSize? MaximumTotalSize { get; set; }

    /// <summary>
    /// Gets or sets the representation written to each physical file. The default is
    /// <see cref="FilesLogFormat.Json"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="FilesLogFormat.Json"/> writes HM Logging JSON Lines, <see cref="FilesLogFormat.Text"/> writes
    /// human-readable text, and <see cref="FilesLogFormat.Clef"/> writes Compact Log Event Format JSON Lines to
    /// <c>.clef</c> files. The selected format is captured when the provider is registered and is not hot-reloaded.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddLoggingFiles(options =&gt;
    /// {
    ///     options.Format = FilesLogFormat.Clef;
    /// });
    /// </code>
    /// </example>
    public FilesLogFormat Format { get; set; } = FilesLogFormat.Json;
}
