namespace ShipLean.Contracts.TaskChanges;

// Reads commits and patches from GitHub or GitLab with the instance token. Patches are cached for a day.
public interface ICodeHost
{
    // sha is 7 to 64 hex characters; a prefix resolves to the full sha in Meta.
    Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default);

    // Against the first parent, or against the empty tree for a root commit. Git unified diff text.
    Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default);

    // The combined base..head.
    Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default);
}
