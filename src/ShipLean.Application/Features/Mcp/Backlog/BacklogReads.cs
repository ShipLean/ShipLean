using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Mcp.Backlog;

// The SQL behind the Backlog tools that no shared read answers (sprint outcomes are ISprintMetrics.GetSprintItemsAsync). Callers resolve and check access first. Points, burndown
// and velocity come from ISprintMetrics, the stack and roll-ups from IWorkTreeReads, never from here.
[RegisterSingleton]
public sealed class BacklogReads(NpgsqlDataSource dataSource)
{
    // The item right after the given one in the project's backlog order, leaving out the item being moved; null when the
    // given item is last. Removed items still hold a place in the order, so they count.
    public async Task<Guid?> NextAfterAsync(Guid projectId, Guid itemId, Guid movingId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT n.id FROM work_item n
            WHERE n.project_id = @projectId AND n.id <> @movingId
              AND n.backlog_order > (SELECT w.backlog_order FROM work_item w WHERE w.id = @itemId)
            ORDER BY n.backlog_order COLLATE "C", n.id
            LIMIT 1
            """,
            new { projectId, itemId, movingId }, cancellationToken: cancellationToken));
    }

    // The features that hold the sprint's stories and bugs (their direct parents), by key.
    public async Task<IReadOnlyList<SprintFeature>> FeaturesAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SprintFeature>(new CommandDefinition(
            """
            SELECT DISTINCT p.id, p.key, p.title, p.number
            FROM work_item w
            JOIN state s ON s.id = w.state_id AND s.category <> 'removed'
            JOIN work_item p ON p.id = w.parent_id AND p.type = 'feature'
            WHERE w.sprint_id = @sprintId AND w.type IN ('story', 'bug')
            ORDER BY p.number
            """,
            new { sprintId }, cancellationToken: cancellationToken));
        return [.. rows];
    }
}
