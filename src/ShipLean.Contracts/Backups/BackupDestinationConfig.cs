namespace ShipLean.Contracts.Backups;

// Where backups go, decrypted, as the destination factory takes it. Provider is "s3", the only one so far, with its
// settings in S3. The record's own ToString is safe to log: S3DestinationConfig hides its secret key.
public sealed record BackupDestinationConfig(string Provider, S3DestinationConfig? S3);
