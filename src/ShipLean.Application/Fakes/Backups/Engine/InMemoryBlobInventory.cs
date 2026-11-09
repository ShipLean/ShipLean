using System.Runtime.CompilerServices;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.Application.Fakes.Backups.Engine;

// Lists the keys of an InMemoryBlobStore.
public sealed class InMemoryBlobInventory(InMemoryBlobStore store) : IBlobInventory
{
    public async IAsyncEnumerable<string> ListKeysAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var key in store.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return key;
        }

        await Task.CompletedTask;
    }
}
