using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Features.Backups.Cli;

// The backup passphrase for the restore verbs: SHIPLEAN_BACKUP_PASSPHRASE when it is set, otherwise one line of input
// with only its line ending removed (docs/backup-file-format.md). It is never written anywhere.
public static class BackupPassphrase
{
    public const string Key = "SHIPLEAN_BACKUP_PASSPHRASE";

    // Null when there is none.
    public static async Task<string?> ReadAsync(IConfiguration configuration, TextReader input, TextWriter error)
    {
        var configured = configuration[Key];
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        await error.WriteLineAsync($"{Key} is not set; reading the passphrase from one line of standard input.");
        var line = await input.ReadLineAsync();
        return string.IsNullOrEmpty(line) ? null : line;
    }
}
