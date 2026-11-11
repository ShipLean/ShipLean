namespace ShipLean.Contracts.Backups.Dtos;

// Where attachments live. InDatabaseBackup: the store is Postgres in the same database, so the dump already holds them.
public sealed record BackupAttachmentStoreDto(BackupAttachmentStoreKind Kind, bool InDatabaseBackup);
