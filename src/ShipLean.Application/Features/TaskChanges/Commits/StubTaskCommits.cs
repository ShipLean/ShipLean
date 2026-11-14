using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Commits;

// TODO: replaced by the store over work_item_commit and code_commit: repository upsert, ICodeHost reads, atomic add, revision, event.
public sealed class StubTaskCommits : ITaskCommits
{
    private static readonly RepoRef Repo = new(CodeProvider.Github, "github.com", "acme/storefront");

    public Task<AddCommitsOutcome> AddAsync(Guid workItemId, IReadOnlyList<CommitRef> commits, CommitSource via, CancellationToken cancellationToken = default)
        => Task.FromResult(new AddCommitsOutcome([.. commits.Select((c, i) => Fake(c.Sha, i + 1))], []));

    public Task<IReadOnlyList<TaskCommit>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaskCommit>>([Fake(new string('b', 40), 1)]);

    private static TaskCommit Fake(string sha, int position)
        => new(Guid.Empty, Repo, sha, position, "Fake commit", "Ada Lovelace", DateTimeOffset.UnixEpoch, 1, 0, ["src/cart.ts"], [new string('a', 40)], CommitSource.AddCommits, "Ada Lovelace", false, DateTimeOffset.UnixEpoch);
}
