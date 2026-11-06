namespace ShipLean.Contracts.Backups.Dtos;

// Which blob store holds the attachments. Wire values localDisk, postgres, s3.
public enum BackupAttachmentStoreKind
{
    LocalDisk,
    Postgres,
    S3,
}
