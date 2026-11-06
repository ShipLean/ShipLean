namespace ShipLean.Contracts.Backups;

// A backup file could not be decrypted: the passphrase is wrong, or the file is damaged, cut off or not a backup.
// The message never holds the passphrase.
public sealed class BackupDecryptException(string message, Exception? inner = null) : Exception(message, inner)
{
}
