namespace Hm.Logging.Providers.Files.Configuration;

internal sealed record FilesProviderSettings(
    string DirectoryPath,
    bool GroupBySource,
    uint? RetentionDays,
    FileSize? MaximumFileSize,
    FileSize? MaximumTotalSize,
    FilesLogFormat Format)
{
    internal static FilesProviderSettings FromOptions(FilesProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.Format))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Format,
                $"The Files provider option 'Format' has unsupported value '{options.Format}'.");
        }

        ValidateSize(options.MaximumFileSize, nameof(options.MaximumFileSize));
        ValidateSize(options.MaximumTotalSize, nameof(options.MaximumTotalSize));
        string configuredPath = string.IsNullOrWhiteSpace(options.DirectoryPath) ? "logs" : options.DirectoryPath;

        return new FilesProviderSettings(
            Path.GetFullPath(configuredPath),
            options.GroupBySource,
            options.RetentionDays,
            options.MaximumFileSize,
            options.MaximumTotalSize,
            options.Format);
    }

    private static void ValidateSize(FileSize? value, string optionName)
    {
        if (value is { Bytes: 0 })
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"The Files provider option '{optionName}' must be greater than zero.");
        }
    }
}
