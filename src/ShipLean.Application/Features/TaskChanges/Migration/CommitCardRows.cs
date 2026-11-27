using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.TaskChanges.Migration;

// The migration's own reads and writes on work_item_link, so the feature does not reach into Links.
public sealed class CommitCardRows(NpgsqlDataSource dataSource)
{
    public sealed record Card(Guid Id, Guid WorkItemId, Guid ProjectId, string Url, string Title);

    // Live URL cards not yet checked, after the given id, on Tasks and Bugs, whose title starts like a commit's (the caller checks the rest).
    public async Task<IReadOnlyList<Card>> NextAsync(Guid after, int count, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Card>(new CommandDefinition(
            """
            SELECT l.id, l.source_id AS WorkItemId, w.project_id AS ProjectId, l.url, l.title
            FROM work_item_link l
            JOIN work_item w ON w.id = l.source_id
            WHERE l.id > @after AND l.link_type = 'url' AND l.archived_at IS NULL AND l.commit_migration_checked_at IS NULL AND l.title LIKE 'commit %'
              AND w.type IN ('task', 'bug')
            ORDER BY l.id
            LIMIT @count
            """,
            new { after, count }, cancellationToken: cancellationToken));
        return [.. rows];
    }

    public async Task<bool> ArchiveAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE work_item_link SET archived_at = @at WHERE id = @id AND archived_at IS NULL",
            new { id, at }, cancellationToken: cancellationToken)) > 0;
    }

    // Records a final answer for a card that stays a link card, so the next run does not read it again.
    public async Task MarkCheckedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE work_item_link SET commit_migration_checked_at = @at WHERE id = @id",
            new { id, at }, cancellationToken: cancellationToken));
    }
}
