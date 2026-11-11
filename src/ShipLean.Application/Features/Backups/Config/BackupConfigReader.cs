using Microsoft.Extensions.Configuration;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Config;

// The saved backup settings, decrypted, for the trigger's per-run snapshot. Null when nothing was ever saved. The
// Backups tab writes the row whole (destination, cron, passphrase), so a row with any of them missing is corrupt.
public sealed class BackupConfigReader(BackupSettingsRows rows, BackupSecrets secrets, BlobStoreChoice blobStore, IConfiguration configuration) : IBackupConfigReader
{
    public async Task<BackupConfig?> ReadAsync(CancellationToken cancellationToken)
    {
        var row = await rows.ReadAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }
        if (row.Provider is null || row.Destination is null || row.Passphrase is null)
        {
            throw new InvalidOperationException("The saved backup settings have no destination or passphrase.");
        }

        var stored = BackupStoredDestination.Parse(row.Destination);
        var secretKey = row.DestinationSecret is null ? null : secrets.Unprotect(row.DestinationSecret);
        var destination = new BackupDestinationConfig(
            row.Provider,
            new S3DestinationConfig(stored.Region, stored.EndpointUrl, stored.Bucket, stored.Prefix, stored.ForcePathStyle, stored.AccessKeyId, secretKey));
        return new BackupConfig(
            row.Enabled,
            destination,
            BackupDestinationId.Of(destination),
            row.Cron,
            row.KeepCount,
            row.IncludeAttachments,
            BackupAttachmentStore.InDatabase(blobStore, configuration),
            secrets.Unprotect(row.Passphrase),
            row.PassphraseGeneration,
            row.UpdatedAt);
    }
}
