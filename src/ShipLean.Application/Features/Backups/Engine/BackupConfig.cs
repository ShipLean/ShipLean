using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Engine;

// One run's snapshot of the backup settings, decrypted: the trigger reads it once and hands it to the job, so a save
// mid-run never changes where or how that run writes. DestinationId is BackupDestinationId.Of(Destination).
// AttachmentsInDatabase: the blob store is Postgres in the same database, so the dump already holds the attachments.
// ToString never prints the passphrase (or, through the destination, the secret key), so logging it is safe.
public sealed record BackupConfig(
    bool Enabled,
    BackupDestinationConfig Destination,
    string DestinationId,
    string Cron,
    int KeepCount,
    bool IncludeAttachments,
    bool AttachmentsInDatabase,
    string Passphrase,
    int PassphraseGeneration,
    DateTimeOffset UpdatedAt)
{
    public override string ToString() =>
        $"BackupConfig {{ Enabled = {Enabled}, Destination = {Destination}, DestinationId = {DestinationId}, Cron = {Cron}, KeepCount = {KeepCount}, IncludeAttachments = {IncludeAttachments}, AttachmentsInDatabase = {AttachmentsInDatabase}, Passphrase = ***, PassphraseGeneration = {PassphraseGeneration}, UpdatedAt = {UpdatedAt:O} }}";
}
