using System.Runtime.CompilerServices;
using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.Application.Storage.Inventory;

// The keys of a LocalDiskBlobStore: every file under its root, as a "/"-separated path relative to the root, streamed from
// a lazy directory walk. A put in flight is a "*.<guid>.tmp" file beside its key, and is not a key.
public sealed class LocalDiskBlobInventory(string root) : IBlobInventory
{
    // ReparsePoint is explicit: the default also skips Hidden, and dotfiles are valid keys. Symlinks are not followed, so a link out of the root or a loop is not a key.
    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private readonly string _root = Path.GetFullPath(root);

    public async IAsyncEnumerable<string> ListKeysAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(_root, "*", Walk))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (path.EndsWith(BlobKey.TempSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            yield return Path.GetRelativePath(_root, path).Replace(Path.DirectorySeparatorChar, '/');
        }

        // An async iterator needs an await; the walk itself is synchronous.
        await Task.CompletedTask;
    }
}
