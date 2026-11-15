using Dapper;
using Npgsql;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Commits;

// Plain storage for work_item_commit, code_commit and code_repository: no access check, no validation, no events.
public sealed class TaskCommitRows(NpgsqlDataSource dataSource)
{
    public sealed record ItemRow(Guid ProjectId, string Type);

    private sealed record CommitQueryRow(
        Guid Id, string Provider, string Host, string Path, string Sha, int Position, string Subject, string AuthorName, DateTime AuthoredAt,
        int Additions, int Deletions, Array Paths, Array ParentShas, string AddedVia, string? AddedByName, string? AddedByAgent, DateTime AddedAt);

    public async Task<ItemRow?> FindItemAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            "SELECT project_id AS ProjectId, type FROM work_item WHERE id = @workItemId", new { workItemId }, cancellationToken: cancellationToken));
    }

    // The keys of the item's Tasks and Bugs, for the message that sends a person from a Story to its tasks.
    public async Task<IReadOnlyList<string>> ChildKeysAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT key FROM work_item WHERE parent_id = @workItemId AND type IN ('task', 'bug') ORDER BY number", new { workItemId }, cancellationToken: cancellationToken))];
    }

    public async Task<IReadOnlyList<TaskCommit>> ListAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CommitQueryRow>(new CommandDefinition(
            """
            SELECT w.id, r.provider, r.host, r.path, w.sha, w.position, c.subject, c.author_name AS AuthorName, c.authored_at AS AuthoredAt,
                   c.additions, c.deletions, c.paths, c.parent_shas AS ParentShas, w.added_via AS AddedVia, u.name AS AddedByName,
                   w.added_by_agent AS AddedByAgent, w.created_at AS AddedAt
            FROM work_item_commit w
            JOIN code_repository r ON r.id = w.repository_id
            JOIN code_commit c ON c.repository_id = w.repository_id AND c.sha = w.sha
            LEFT JOIN app_user u ON u.id = w.added_by
            WHERE w.work_item_id = @workItemId AND w.archived_at IS NULL
            ORDER BY w.position
            """,
            new { workItemId }, cancellationToken: cancellationToken));
        return [.. rows.Select(ToCommit)];
    }

    // Writes the commits in one transaction under a lock on the item, so two calls serialise. Answers the ids it inserted;
    // a commit the item already has is not in it.
    public async Task<IReadOnlySet<Guid>> InsertAsync(Guid workItemId, IReadOnlyList<(RepoRef Repo, CommitMeta Meta)> commits, CommitSource via, Guid? addedBy, string? addedByAgent, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT 1", new { workItemId }, transaction, cancellationToken: cancellationToken));
        var position = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT coalesce(max(position), 0) FROM work_item_commit WHERE work_item_id = @workItemId", new { workItemId }, transaction, cancellationToken: cancellationToken));
        var inserted = new HashSet<Guid>();
        foreach (var (repo, meta) in commits)
        {
            var repositoryId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                """
                INSERT INTO code_repository (id, provider, host, path) VALUES (@id, @provider, @host, @path)
                ON CONFLICT (provider, host, path) DO UPDATE SET host = excluded.host
                RETURNING id
                """,
                new { id = Guid.NewGuid(), provider = repo.Provider.ToString().ToLowerInvariant(), host = repo.Host, path = repo.Path }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at)
                VALUES (@repositoryId, @sha, @parents, @subject, @author, @authoredAt, @additions, @deletions, @filesChanged, @paths, now())
                ON CONFLICT DO NOTHING
                """,
                new
                {
                    repositoryId,
                    sha = meta.Sha,
                    parents = meta.ParentShas.ToArray(),
                    subject = meta.Subject,
                    author = meta.AuthorName,
                    authoredAt = meta.AuthoredAt.ToUniversalTime(),
                    additions = meta.Additions,
                    deletions = meta.Deletions,
                    filesChanged = meta.FilesChanged,
                    paths = meta.Paths.ToArray(),
                }, transaction, cancellationToken: cancellationToken));
            var id = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                """
                INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via, added_by, added_by_agent)
                VALUES (@id, @workItemId, @repositoryId, @sha, @position, @via, @addedBy, @addedByAgent)
                ON CONFLICT DO NOTHING
                RETURNING id
                """,
                new { id = Guid.NewGuid(), workItemId, repositoryId, sha = meta.Sha, position = position + 1, via = Via(via), addedBy, addedByAgent }, transaction, cancellationToken: cancellationToken));
            if (id is { } added)
            {
                position++;
                inserted.Add(added);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    private static TaskCommit ToCommit(CommitQueryRow r) => new(
        r.Id, new RepoRef(Enum.Parse<CodeProvider>(r.Provider, ignoreCase: true), r.Host, r.Path), r.Sha, r.Position, r.Subject, r.AuthorName, Utc(r.AuthoredAt),
        r.Additions, r.Deletions, (string[])r.Paths, (string[])r.ParentShas, ViaOf(r.AddedVia), r.AddedByAgent ?? r.AddedByName ?? string.Empty, r.AddedByAgent is not null, Utc(r.AddedAt));

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static string Via(CommitSource source) => source switch
    {
        CommitSource.ReportTask => "report_task",
        CommitSource.AddCommits => "add_commits",
        CommitSource.Form => "form",
        _ => "migration",
    };

    private static CommitSource ViaOf(string text) => text switch
    {
        "report_task" => CommitSource.ReportTask,
        "add_commits" => CommitSource.AddCommits,
        "form" => CommitSource.Form,
        _ => CommitSource.Migration,
    };
}
