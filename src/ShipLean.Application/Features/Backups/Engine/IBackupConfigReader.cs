namespace ShipLean.Application.Features.Backups.Engine;

// The saved backup settings, decrypted, or null when none are saved. The trigger reads it once per run and hands the
// snapshot on (BackupConfig).
public interface IBackupConfigReader
{
    Task<BackupConfig?> ReadAsync(CancellationToken cancellationToken);
}
