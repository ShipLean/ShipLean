using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// Sends each read to the reader for the repository's provider.
public sealed class CodeHostRouter(GitHubCodeHost github, GitLabCodeHost gitlab) : ICodeHost
{
    public Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => For(repo).ReadCommitAsync(repo, sha, cancellationToken);

    public Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => For(repo).ReadPatchAsync(repo, sha, cancellationToken);

    public Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default)
        => For(repo).ReadRangePatchAsync(repo, baseSha, headSha, cancellationToken);

    private ICodeHost For(RepoRef repo) => repo.Provider == CodeProvider.Gitlab ? gitlab : github;
}
