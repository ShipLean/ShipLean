namespace ShipLean.Contracts.Backups;

// A backup destination call failed, or the destination config names a provider nobody makes. The message is
// person-readable and never holds a secret key, a passphrase or a full connection string.
public sealed class BackupDestinationException(string message, Exception? inner = null) : Exception(message, inner)
{
}
