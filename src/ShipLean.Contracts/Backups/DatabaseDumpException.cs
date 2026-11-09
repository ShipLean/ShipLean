namespace ShipLean.Contracts.Backups;

// The database dump failed. The message is person-readable and never holds a password or a full connection string.
public sealed class DatabaseDumpException(string message, Exception? inner = null) : Exception(message, inner)
{
}
