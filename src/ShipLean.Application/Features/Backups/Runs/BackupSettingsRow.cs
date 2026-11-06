namespace ShipLean.Application.Features.Backups.Runs;

// The backup_settings row as stored: Destination is the jsonb as text, DestinationSecret and Passphrase are still Data
// Protection ciphertext. Only the Backups tab's admin writes it, and only its config reader decrypts it.
public sealed record BackupSettingsRow(
    bool Enabled,
    string? Provider,
    string? Destination,
    string? DestinationSecret,
    string? SecretLast4,
    string Cron,
    int KeepCount,
    bool IncludeAttachments,
    string? Passphrase,
    int PassphraseGeneration,
    DateTimeOffset? PassphraseSetAt,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy)
{
    public override string ToString() =>
        $"BackupSettingsRow {{ Enabled = {Enabled}, Provider = {Provider}, Destination = {Destination}, DestinationSecret = ***, SecretLast4 = {SecretLast4}, Cron = {Cron}, KeepCount = {KeepCount}, IncludeAttachments = {IncludeAttachments}, Passphrase = ***, PassphraseGeneration = {PassphraseGeneration}, PassphraseSetAt = {PassphraseSetAt:O}, UpdatedAt = {UpdatedAt:O}, UpdatedBy = {UpdatedBy} }}";
}
