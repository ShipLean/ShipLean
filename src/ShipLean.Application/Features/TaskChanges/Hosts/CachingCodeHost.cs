using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// Patches for a day, and a Missing answer for five minutes (the commit may be pushed a moment later). An Unreadable answer is
// never kept: the token or the network may be fixed at once. A commit's meta is read when it is added and not cached.
public sealed class CachingCodeHost(ICodeHost inner, ICodePatchCache cache) : ICodeHost
{
    public static readonly TimeSpan PatchLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan MissingLifetime = TimeSpan.FromMinutes(5);

    public Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => inner.ReadCommitAsync(repo, sha, cancellationToken);

    public Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => CachedAsync($"{Key(repo)}:{sha.ToLowerInvariant()}", () => inner.ReadPatchAsync(repo, sha, cancellationToken), cancellationToken);

    public Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default)
        => CachedAsync($"{Key(repo)}:{baseSha.ToLowerInvariant()}..{headSha.ToLowerInvariant()}", () => inner.ReadRangePatchAsync(repo, baseSha, headSha, cancellationToken), cancellationToken);

    private static string Key(RepoRef repo) => $"{repo.Provider}:{repo.Host}:{repo.Path}";

    private async Task<PatchRead> CachedAsync(string key, Func<Task<PatchRead>> read, CancellationToken cancellationToken)
    {
        if (await cache.GetAsync(key, cancellationToken) is { } hit)
        {
            return hit;
        }

        var patch = await read();
        if (patch.Status != CommitReadStatus.Unreadable)
        {
            await cache.SetAsync(key, patch, patch.Status == CommitReadStatus.Found ? PatchLifetime : MissingLifetime, cancellationToken);
        }

        return patch;
    }
}
