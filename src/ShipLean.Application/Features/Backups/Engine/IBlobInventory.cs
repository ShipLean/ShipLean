namespace ShipLean.Application.Features.Backups.Engine;

// Every key in the active blob store (the one BlobStoreChoice names), in no particular order and never a ".tmp" key
// (BlobKey.TempSuffix: a store's own temporary files). A separate interface, so IBlobStore and its doubles stay as they are.
public interface IBlobInventory
{
    IAsyncEnumerable<string> ListKeysAsync(CancellationToken cancellationToken);
}
