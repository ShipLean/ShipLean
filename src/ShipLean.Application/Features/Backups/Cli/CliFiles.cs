namespace ShipLean.Application.Features.Backups.Cli;

// File rules shared by the restore verbs.
internal static class CliFiles
{
    public const string Suffix = ".slbk";

    // True for a file, a folder or a link (even a dangling one) at the path.
    public static bool Occupied(string path)
        => File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;

    public static bool IsBackupObject(string path) => path.EndsWith(Suffix, StringComparison.Ordinal);

    public static string StripSuffix(string path) => path[..^Suffix.Length];

    // The backup objects under a folder, in a stable order.
    public static IReadOnlyList<string> ObjectsUnder(string folder)
        => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(IsBackupObject)
            .Order(StringComparer.Ordinal)
            .ToList();

    // A new file nobody else can have opened first: CreateNew refuses an existing file or link, owner-only on Unix.
    public static FileStream CreateNewPrivate(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(path, options);
    }
}
