namespace ShipLean.Contracts.Backups.Dtos;

// PUT /api/instance/backups. Passphrase null keeps the saved one, and is required when none is saved.
public sealed record BackupSaveDto(
    bool Enabled,
    BackupDestinationSaveDto Destination,
    string Cron,
    int KeepCount,
    bool IncludeAttachments,
    string? Passphrase);
