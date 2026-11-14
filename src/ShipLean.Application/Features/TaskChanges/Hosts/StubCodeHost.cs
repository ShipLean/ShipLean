using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// TODO: replaced by the GitHub and GitLab readers over the named HttpClient with the instance token, with the one-day patch cache.
public sealed class StubCodeHost : ICodeHost
{
    private const string Diff = "diff --git a/src/cart.ts b/src/cart.ts\n--- a/src/cart.ts\n+++ b/src/cart.ts\n@@ -1,2 +1,3 @@\n const a = 1;\n+const b = 2;\n const c = 3;\n";

    public Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
    {
        var full = sha.Length >= 40 ? sha : sha.PadRight(40, '0');
        var meta = new CommitMeta(full, [new string('a', 40)], "Fake commit", "Ada Lovelace", DateTimeOffset.UnixEpoch, 1, 0, ["src/cart.ts"]);
        return Task.FromResult(new CommitRead(CommitReadStatus.Found, meta, null));
    }

    public Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => Task.FromResult(new PatchRead(CommitReadStatus.Found, Diff));

    public Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default)
        => Task.FromResult(new PatchRead(CommitReadStatus.Found, Diff));
}
