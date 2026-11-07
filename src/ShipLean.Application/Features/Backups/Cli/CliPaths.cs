namespace ShipLean.Application.Features.Backups.Cli;

// Path rules the restore verbs share.
public static class CliPaths
{
    public const string EncryptedSuffix = ".slbk";
    public const string PartialSuffix = ".partial";

    // True when anything is at path: a file, a folder, or a link (even one whose target is missing).
    public static bool Taken(string path)
        => File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;

    // Every .slbk file under folder, at any depth, hidden ones included, in ordinal order of their paths.
    public static IReadOnlyList<string> EncryptedFiles(string folder)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false };
        return Directory.EnumerateFiles(folder, "*", options)
            .Where(path => path.EndsWith(EncryptedSuffix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
