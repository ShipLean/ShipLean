using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.Application.Storage.Inventory;

// The keys of a LocalDiskBlobStore: every file under its root, as a "/"-separated path relative to the root, streamed from
// a lazy directory walk. A put in flight is a "*.<guid>.tmp" file beside its key, and is not a key.
public sealed class LocalDiskBlobInventory(string root) : IBlobInventory
{
    private readonly string _root = Path.GetFullPath(root);

    public async IAsyncEnumerable<string> ListKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (path.EndsWith(BlobKey.TempSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            yield return Path.GetRelativePath(_root, path).Replace(Path.DirectorySeparatorChar, '/');
        }

        await Task.CompletedTask;
    }
}
