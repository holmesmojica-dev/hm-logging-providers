using System.Globalization;
using System.Text.RegularExpressions;

namespace Hm.Logging.Providers.Files;

internal sealed partial record ManagedLogFile(
    string Path,
    DateOnly DateUtc,
    int Segment,
    string Prefix,
    string Extension,
    string? SourceDirectory,
    ulong Length)
{
    private static readonly Regex FileNamePattern = MyRegex();

    internal static IReadOnlyList<ManagedLogFile> Discover(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var files = new List<ManagedLogFile>();
        AddRecognizedFiles(rootDirectory, rootDirectory, "logs", sourceDirectory: null, files);
        foreach (string directory in Directory.EnumerateDirectories(rootDirectory))
        {
            string prefix = System.IO.Path.GetFileName(directory);
            if (IsSafePrefix(prefix))
            {
                AddRecognizedFiles(rootDirectory, directory, prefix, directory, files);
            }
        }

        return files;
    }

    internal static bool TryParse(string rootDirectory, string path, out ManagedLogFile? managedFile)
    {
        string fileName = System.IO.Path.GetFileName(path);
        Match match = FileNamePattern.Match(fileName);
        if (!match.Success || !DateOnly.TryParseExact(
                match.Groups["date"].Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
        {
            managedFile = null;
            return false;
        }

        string relative = System.IO.Path.GetRelativePath(rootDirectory, path);
        string[] parts = relative.Split([System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        string prefix = match.Groups["prefix"].Value;
        string? sourceDirectory;
        if (parts.Length == 1)
        {
            if (!string.Equals(prefix, "logs", StringComparison.Ordinal))
            {
                managedFile = null;
                return false;
            }

            sourceDirectory = null;
        }
        else if (parts.Length == 2 && string.Equals(parts[0], prefix, StringComparison.Ordinal) && IsSafePrefix(prefix))
        {
            sourceDirectory = System.IO.Path.GetDirectoryName(path);
        }
        else
        {
            managedFile = null;
            return false;
        }

        int segment = 0;
        if (match.Groups["segment"].Success
            && !int.TryParse(
                match.Groups["segment"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out segment))
        {
            managedFile = null;
            return false;
        }

        long length = new FileInfo(path).Length;
        managedFile = new ManagedLogFile(
            path,
            date,
            segment,
            prefix,
            match.Groups["extension"].Value,
            sourceDirectory,
            checked((ulong)length));
        return true;
    }

    private static void AddRecognizedFiles(
        string rootDirectory,
        string directory,
        string prefix,
        string? sourceDirectory,
        List<ManagedLogFile> files)
    {
        foreach (string path in Directory.EnumerateFiles(directory))
        {
            if (TryParse(rootDirectory, path, out ManagedLogFile? file)
                && file is not null
                && string.Equals(file.Prefix, prefix, StringComparison.Ordinal)
                && string.Equals(file.SourceDirectory, sourceDirectory, StringComparison.Ordinal))
            {
                files.Add(file);
            }
        }
    }

    private static bool IsSafePrefix(string value)
    {
        return FileNamePattern.IsMatch($"{value}-2000-01-01.log");
    }

    [GeneratedRegex("^(?<prefix>[a-z0-9]+(?:-[a-z0-9]+)*)-(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})(?:\\.(?<segment>[1-9][0-9]*))?\\.(?<extension>jsonl|log)$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
}
