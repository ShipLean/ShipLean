using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;

namespace ShipLean.Application.Features.Taskboard;

// The tasks on a sprint's Taskboard: the live tasks under the sprint's stories and bugs, and the unparented tasks scheduled
// into the sprint. Ordered like the store's cell peers (`column_order NULLS LAST, id`); those only compare within one (state, parent) cell, which is how callers group them.
[RegisterPerRequest]
public sealed class TaskboardTasks(NpgsqlDataSource dataSource, TimeProvider time)
{
    public async Task<IReadOnlyList<StackRowDto>> ForSprintAsync(Guid projectId, Guid sprintId, IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken)
    {
        var sql =
            $"""
            SELECT w.id, w.key, w.title, w.story_points AS points, w.changed_at AS changedAt,
                   s.id AS stateId, s.name AS stateName, s.category AS stateCategory,
                   sp.id AS sprintId, sp.name AS sprintName, to_char(sp.start_date, 'YYYY-MM-DD') AS sprintStart, to_char(sp.end_date, 'YYYY-MM-DD') AS sprintEnd,
                   u.id AS assigneeId, u.name AS assigneeName, u.initials AS assigneeInitials, u.avatar_colour AS assigneeColour,
                   {UserRefs.PhotoUrlSql} AS assigneePhotoUrl,
                   p.id AS parentId, p.key AS parentKey, p.type AS parentType, p.title AS parentTitle,
                   w.tags AS tags, w.revision AS revision
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            LEFT JOIN sprint sp ON sp.id = w.sprint_id
            LEFT JOIN app_user u ON u.id = w.assigned_to
            LEFT JOIN work_item p ON p.id = w.parent_id AND p.project_id = w.project_id
            WHERE w.project_id = @projectId AND w.type = 'task' AND s.category <> 'removed'
              AND (w.parent_id = ANY(@parentIds) OR (w.parent_id IS NULL AND w.sprint_id = @sprintId))
            ORDER BY w.column_order NULLS LAST, w.id
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TaskboardTaskRow>(new CommandDefinition(sql, new { projectId, sprintId, parentIds = parentIds.ToArray() }, cancellationToken: cancellationToken));
        var today = SprintLabels.Today(time);
        return [.. rows.Select(row => row.ToRow(today))];
    }
}
