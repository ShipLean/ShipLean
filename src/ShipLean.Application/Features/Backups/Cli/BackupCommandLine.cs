namespace ShipLean.Application.Features.Backups.Cli;

// The parsed arguments of a backup verb: `decrypt-backup --in <file|folder> --out <file|folder>` or
// `restore-blobs --in <folder> [--overwrite]`.
public sealed record BackupCommandLine(string Verb, string In, string? Out, bool Overwrite)
{
    public const string DecryptVerb = "decrypt-backup";
    public const string RestoreVerb = "restore-blobs";

    public const string UsageText =
        "Usage:\n"
        + "  decrypt-backup --in <file|folder> --out <file|folder>\n"
        + "      Decrypts a backup file, or every .slbk file under a folder (the .slbk is dropped from each name).\n"
        + "  restore-blobs --in <folder> [--overwrite]\n"
        + "      Puts every attachment .slbk object under the folder into the blob store SHIPLEAN_BLOB_* select,\n"
        + "      skipping keys it already holds unless --overwrite is given.\n"
        + "The passphrase comes from SHIPLEAN_BACKUP_PASSPHRASE, or one line on standard input.\n"
        + "Exit codes: 0 ok, 1 wrong passphrase, damaged file or store error, 2 usage.";

    // Null with a problem when the arguments don't make a command.
    public static BackupCommandLine? Parse(IReadOnlyList<string> args, out string problem)
    {
        problem = "";
        if (args.Count == 0 || args[0] is not (DecryptVerb or RestoreVerb))
        {
            problem = "Give a verb: decrypt-backup or restore-blobs.";
            return null;
        }

        var verb = args[0];
        string? inPath = null;
        string? outPath = null;
        var overwrite = false;
        var i = 1;
        while (i < args.Count)
        {
            var option = args[i];
            if (option == "--overwrite" && verb == RestoreVerb && !overwrite)
            {
                overwrite = true;
                i++;
                continue;
            }

            var isPathOption = option == "--in" || (option == "--out" && verb == DecryptVerb);
            if (!isPathOption || i + 1 == args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                problem = $"{verb}: unexpected or incomplete argument '{option}'.";
                return null;
            }

            if ((option == "--in" ? inPath : outPath) is not null)
            {
                problem = $"{verb}: {option} is given twice.";
                return null;
            }

            if (option == "--in")
            {
                inPath = args[i + 1];
            }
            else
            {
                outPath = args[i + 1];
            }

            i += 2;
        }

        if (inPath is null || (verb == DecryptVerb && outPath is null))
        {
            problem = verb == DecryptVerb ? $"{verb}: --in and --out are required." : $"{verb}: --in is required.";
            return null;
        }

        return new BackupCommandLine(verb, inPath, outPath, overwrite);
    }
}
