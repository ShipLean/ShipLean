namespace ShipLean.Contracts.Backups.Dtos;

// The destination as PUT sends it, and the body of POST /api/instance/backups/test. Its
// ToString is safe to log: S3DestinationSaveDto hides the secret key.
public sealed record BackupDestinationSaveDto(BackupProvider Provider, S3DestinationSaveDto? S3);
