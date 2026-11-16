namespace ShipLean.Contracts.TaskChanges;

// Turns what a person or an agent types into a repository and a commit. The provider comes from the instance's
// integration settings, so a host that is neither GitHub nor the configured GitLab is refused.
public interface IRepoAddresses
{
    // git@host:o/r.git, https://host/o/r(.git), ssh://git@host/o/r and host/o/r. GitLab subgroups are allowed.
    // Throws repo_unreadable for an unknown host and Validation (field "repo") for a string that does not parse.
    Task<RepoRef> ParseAsync(string repo, CancellationToken cancellationToken = default);

    // GitHub /o/r/commit/sha and GitLab /o/r/-/commit/sha. Anything else throws Validation (field "sha").
    Task<CommitUrlParts> ParseCommitUrlAsync(string url, CancellationToken cancellationToken = default);
}
