namespace ShipLean.Application.Features.Backups.Mirror;

// The inventory lists a blob the store can't read. The message names the blob key (a generated id, not a secret),
// so the run's stored error says which blob stopped the backup.
public sealed class BlobUnreadableException(string blobKey) : Exception($"Blob '{blobKey}' is listed but can't be read.")
{
    public string BlobKey { get; } = blobKey;
}
