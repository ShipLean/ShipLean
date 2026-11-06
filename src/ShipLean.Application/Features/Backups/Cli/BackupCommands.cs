namespace ShipLean.Application.Features.Backups.Cli;

// The restore verbs of the image (`decrypt-backup`, `restore-blobs`). Program.cs hands them the arguments before it
// builds the web host, so they never start the app. Exit codes: 0 ok, 1 wrong passphrase, damaged file or store
// error, 2 usage.
public static class BackupCommands
{
    public static bool Handles(string verb) => verb is "decrypt-backup" or "restore-blobs";

    // TODO: stub; the decrypt-backup and restore-blobs verbs replace it.
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextReader input, TextWriter output, TextWriter error)
    {
        await error.WriteLineAsync($"{args[0]}: not implemented");
        return 2;
    }
}
