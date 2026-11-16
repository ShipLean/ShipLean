namespace ShipLean.Contracts.TaskChanges;

// The commits on a Task or Bug. Callers check IProjectAccess first; this trusts them.
public interface ITaskCommits
{
    // Atomic per call: any error and nothing is written. At most 25 commits. Throws not_task_or_bug for any other type,
    // bad_sha for a sha that is not 7 to 64 hex characters or a commit that is Missing, and repo_unreadable for a repository
    // the token can't read. Skips a commit the item already has. Writes one commits revision, publishes commitsChanged and
    // calls ICodeComments.MarkOutdatedAsync with what was added; Migration does none of the three.
    Task<AddCommitsOutcome> AddAsync(Guid workItemId, IReadOnlyList<CommitRef> commits, CommitSource via, CancellationToken cancellationToken = default);

    // The item's live commits in position order.
    Task<IReadOnlyList<TaskCommit>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);
}
