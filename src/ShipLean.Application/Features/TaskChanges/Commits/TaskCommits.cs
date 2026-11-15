using System.Text.RegularExpressions;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.TaskChanges.Commits;

// ITaskCommits over TaskCommitRows. Every commit is validated and read from the host before anything is written; the writes
// are one transaction. The revision, the event and the outdated marks come after the commit, and only for a call that added something.
public sealed partial class TaskCommits(
    TaskCommitRows rows, IRepoAddresses addresses, ICodeHost host, IRevisionNotes notes, ICodeComments comments,
    IProjectEvents events, ICurrentUser currentUser, ICurrentAgent currentAgent) : ITaskCommits
{
    private const int MaxCommits = 25;

    public async Task<AddCommitsOutcome> AddAsync(Guid workItemId, IReadOnlyList<CommitRef> commits, CommitSource via, CancellationToken cancellationToken = default)
    {
        if (commits.Count > MaxCommits)
        {
            throw ApiException.Validation($"Send at most {MaxCommits} commits at a time.", "commits");
        }

        var item = await rows.FindItemAsync(workItemId, cancellationToken) ?? throw ApiException.NotFound("That work item does not exist.");
        if (item.Type is not ("task" or "bug"))
        {
            throw ApiException.NotTaskOrBug(await NotTaskOrBugMessageAsync(workItemId, item.Type, cancellationToken));
        }

        var read = await ReadAllAsync(commits, cancellationToken);
        if (read.Count == 0)
        {
            return new AddCommitsOutcome([], []);
        }

        // A migration runs in a startup job with no request, so there is no user to read.
        Guid? addedBy = via == CommitSource.Migration ? null : currentUser.Id;
        var inserted = await rows.InsertAsync(workItemId, read, via, addedBy, AgentStamp.Of(currentAgent), cancellationToken);
        var all = await rows.ListAsync(workItemId, cancellationToken);
        var wanted = read.Select(r => (r.Repo.Path, r.Meta.Sha)).ToHashSet();
        var added = all.Where(c => inserted.Contains(c.Id)).ToList();
        var skipped = all.Where(c => !inserted.Contains(c.Id) && wanted.Contains((c.Repo.Path, c.Sha))).ToList();

        if (via != CommitSource.Migration && added.Count > 0)
        {
            foreach (var one in added) { await notes.AddAsync(workItemId, FieldIds.Commits, Label([one]), WriteOptions.User, cancellationToken); }
            await events.PublishAsync(new CommitsChanged(item.ProjectId, workItemId));
            await comments.MarkOutdatedAsync(workItemId, added, cancellationToken);
        }

        return new AddCommitsOutcome(added, skipped);
    }

    public Task<IReadOnlyList<TaskCommit>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default) => rows.ListAsync(workItemId, cancellationToken);

    private async Task<List<(RepoRef Repo, CommitMeta Meta)>> ReadAllAsync(IReadOnlyList<CommitRef> commits, CancellationToken cancellationToken)
    {
        var read = new List<(RepoRef Repo, CommitMeta Meta)>();
        var parsed = new Dictionary<string, RepoRef>(StringComparer.Ordinal);
        foreach (var commit in commits)
        {
            var sha = commit.Sha?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!ShaPattern().IsMatch(sha))
            {
                throw ApiException.BadSha("A commit sha is 7 to 64 hex characters.");
            }

            if (!parsed.TryGetValue(commit.Repo, out var repo))
            {
                repo = await addresses.ParseAsync(commit.Repo, cancellationToken);
                parsed[commit.Repo] = repo;
            }

            var result = await host.ReadCommitAsync(repo, sha, cancellationToken);
            switch (result.Status)
            {
                case CommitReadStatus.Unreadable:
                    throw ApiException.RepoUnreadable($"ShipLean can't read {repo.Name}. Check that the instance token can see it.");
                case CommitReadStatus.Missing:
                    throw ApiException.BadSha($"{repo.Name} has no commit {sha}.");
            }

            var meta = result.Meta!;
            if (!read.Any(r => r.Repo.Path == repo.Path && r.Meta.Sha == meta.Sha))
            {
                read.Add((repo, meta));
            }
        }

        return read;
    }

    private async Task<string> NotTaskOrBugMessageAsync(Guid workItemId, string type, CancellationToken cancellationToken)
    {
        var keys = type == "story" ? await rows.ChildKeysAsync(workItemId, cancellationToken) : [];
        return keys.Count > 0
            ? $"Commits go on a Task or Bug, not a {type}. Add them to one of its tasks: {string.Join(", ", keys)}."
            : $"Commits go on a Task or Bug, not a {type}.";
    }

    private static string Label(IReadOnlyList<TaskCommit> added)
        => $"Commits +{added.Count} ({string.Join(", ", added.Select(c => c.Repo.Name).Distinct())})";

    [GeneratedRegex("^[0-9a-f]{7,64}$")]
    private static partial Regex ShaPattern();
}
