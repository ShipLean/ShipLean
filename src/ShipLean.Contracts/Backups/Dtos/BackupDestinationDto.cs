namespace ShipLean.Contracts.Backups.Dtos;

// The saved destination as GET returns it: the provider plus that provider's settings.
public sealed record BackupDestinationDto(BackupProvider Provider, S3DestinationDto? S3);
