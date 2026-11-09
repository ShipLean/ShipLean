namespace ShipLean.Application.Features.Backups.Cli;

// The blob store failed on one object; the message names the key and never holds a secret.
public sealed class BlobStoreRestoreException(string message, Exception inner) : Exception(message, inner)
{
}
