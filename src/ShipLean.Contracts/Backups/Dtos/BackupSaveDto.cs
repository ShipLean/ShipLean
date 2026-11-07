namespace ShipLean.Contracts.Backups.Dtos;

// PUT /api/instance/backups. Passphrase null keeps the saved one, and is required when none is saved. ToString never
// prints the passphrase, nor (through S3DestinationSaveDto) the secret key: MVC logs action arguments at Trace.
public sealed record BackupSaveDto(
    bool Enabled,
    BackupDestinationSaveDto Destination,
    string Cron,
    int KeepCount,
    bool IncludeAttachments,
    string? Passphrase)
{
    public override string ToString() =>
        $"BackupSaveDto {{ Enabled = {Enabled}, Destination = {Destination}, Cron = {Cron}, KeepCount = {KeepCount}, IncludeAttachments = {IncludeAttachments}, Passphrase = {(Passphrase is null ? "" : "***")} }}";
}
