namespace ShipLean.Contracts.Backups;

// One object in a backup destination: its whole key and its size in bytes.
public sealed record BackupObject(string Key, long Size);
