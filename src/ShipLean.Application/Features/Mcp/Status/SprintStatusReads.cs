using System.Globalization;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Mcp.Status;

// What sprint_status shows that no shared read gives: the sprint's goal and dates, its live items with their state and
// assignee, and which of them are blocked. Blocked = the tag Blocked (any case) or a blockedBy link to an item that is
// not yet Completed or Removed. Points, burndown and capacity come from ISprintMetrics and ISprintStore, never from here.
[RegisterSingleton]
public sealed class SprintStatusReads(NpgsqlDataSource dataSource)
{
    // Throws ApiException.NotFound (unknown sprint).
    public async Task<SprintHead> HeadAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<HeadRow>(new CommandDefinition(
            "SELECT name, goal, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate FROM sprint WHERE id = @sprintId",
            new { sprintId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No sprint with that name was found.");
        return new SprintHead(row.Name, row.Goal, Parse(row.StartDate), Parse(row.EndDate));
    }

    // The sprint's items that are not Removed, by state category, state order, then backlog order.
    public async Task<IReadOnlyList<SprintItem>> ItemsAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ItemRow>(new CommandDefinition(
            """
            SELECT w.key, w.title, w.type, s.name AS stateName, s.category AS stateCategory, w.story_points AS points,
                   u.name AS assignee, w.parent_id AS parentId, w.tags,
                   COALESCE((SELECT array_agg(t.key ORDER BY t.key)
                             FROM work_item_link l
                             JOIN work_item t ON t.id = l.target_id
                             JOIN state ts ON ts.id = t.state_id
                             WHERE l.source_id = w.id AND l.link_type = 'blockedBy' AND l.archived_at IS NULL
                               AND ts.category NOT IN ('completed', 'removed')), '{}') AS blockedBy
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            LEFT JOIN app_user u ON u.id = w.assigned_to
            WHERE w.sprint_id = @sprintId AND s.category <> 'removed'
            ORDER BY CASE s.category WHEN 'proposed' THEN 0 WHEN 'inProgress' THEN 1 WHEN 'resolved' THEN 2 ELSE 3 END,
                     s.sort_order, s.name, w.backlog_order COLLATE "C", w.id
            """,
            new { sprintId }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => row.ToItem())];
    }

    private static DateOnly Parse(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record HeadRow(string Name, string Goal, string StartDate, string EndDate);

    // Tags and BlockedBy are Arrays: Npgsql reports text[] as one, and it is always a string[].
    private sealed record ItemRow(string Key, string Title, string Type, string StateName, string StateCategory, decimal? Points, string? Assignee, Guid? ParentId, Array Tags, Array BlockedBy)
    {
        public SprintItem ToItem()
        {
            var tags = (string[])Tags;
            var blockedBy = (string[])BlockedBy;
            var blocked = blockedBy.Length > 0 || tags.Any(tag => string.Equals(tag, "Blocked", StringComparison.OrdinalIgnoreCase));
            return new SprintItem(Key, Title, Type, StateName, StateCategory, Points, Assignee, ParentId is null, blocked, blockedBy);
        }
    }
}
