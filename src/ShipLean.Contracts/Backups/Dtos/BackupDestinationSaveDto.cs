namespace ShipLean.Contracts.Backups.Dtos;

// The destination as PUT sends it, and the body of POST /api/instance/backups/test.
public sealed record BackupDestinationSaveDto(BackupProvider Provider, S3DestinationSaveDto? S3);
