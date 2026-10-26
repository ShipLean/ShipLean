using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Boards.View;

// The board's own reads: the cards with their board position, the Epic and Feature links, state and display names, and the
// user's lane preference. Descendants and roll-ups are not here: they come from IWorkTreeReads.
[RegisterPerRequest]
public sealed class BoardReads(NpgsqlDataSource dataSource, ICurrentUser currentUser)
{
    // The key of the per-user, per-board lane preference (a JSON string): "on" or "off" on the Stories board, "epic" or "none" on the Features board.
    public static string LanePreferenceKey(Guid boardId) => $"view:board-lanes:{boardId}";

    private const string CardSql =
        $"""
        SELECT w.id, w.key, w.title, w.type,
               s.id AS stateId, s.name AS stateName, s.category AS stateCategory,
               u.id AS assigneeId, u.name AS assigneeName, u.initials AS assigneeInitials, u.avatar_colour AS assigneeColour,
               {UserRefs.PhotoUrlSql} AS assigneePhotoUrl,
               w.story_points AS points, w.priority::int AS priority, w.severity::int AS severity,
               sp.id AS sprintId, sp.name AS sprintName,
               p.id AS parentId, p.key AS parentKey, p.type AS parentType, p.title AS parentTitle,
               w.tags AS tags, w.board_column_id AS columnId, w.board_column_done AS columnDone, w.swimlane_id AS laneId,
               w.column_order AS columnOrder, w.revision AS revision,
               w.created_at AS createdAt, cb.id AS createdById, cb.name AS createdByName,
               w.changed_at AS changedAt, ch.id AS changedById, ch.name AS changedByName,
               w.state_changed_at AS stateChangedAt, w.original_system AS originalSystem, w.original_system_id AS originalSystemId
        FROM work_item w
        JOIN state s ON s.id = w.state_id
        LEFT JOIN sprint sp ON sp.id = w.sprint_id
        LEFT JOIN app_user u ON u.id = w.assigned_to
        LEFT JOIN work_item p ON p.id = w.parent_id AND p.project_id = w.project_id
        LEFT JOIN app_user cb ON cb.id = w.created_by
        LEFT JOIN app_user ch ON ch.id = w.changed_by
        WHERE w.project_id = @projectId AND w.type = ANY(@types) AND s.category <> 'removed'
          AND ((@only::uuid IS NULL AND w.board_column_id IS NOT NULL) OR w.id = @only)
        ORDER BY w.backlog_order, w.id
        """;

    // The live cards of the given types that sit on the board, or just the one card (placed or not).
    public async Task<IReadOnlyList<BoardCardRow>> CardsAsync(Guid projectId, IReadOnlyList<string> types, Guid? only, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<BoardCardRow>(new CommandDefinition(CardSql, new { projectId, types = types.ToArray(), only }, cancellationToken: cancellationToken))];
    }

    // Every Epic and Feature of the project, Removed ones too: a Removed Feature still links the Features below it to their Epic.
    public async Task<IReadOnlyList<BoardHierarchyRow>> HierarchyAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<BoardHierarchyRow>(new CommandDefinition(
            """
            SELECT w.id, w.key, w.title, w.type, w.parent_id AS parentId, s.category AS stateCategory, w.backlog_order AS backlogOrder
            FROM work_item w JOIN state s ON s.id = w.state_id
            WHERE w.project_id = @projectId AND w.type IN ('epic', 'feature')
            ORDER BY w.backlog_order, w.id
            """,
            new { projectId }, cancellationToken: cancellationToken))];
    }

    public async Task<IReadOnlyList<BoardStateRow>> StatesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<BoardStateRow>(new CommandDefinition(
            "SELECT id, type, name, category FROM state WHERE id = ANY(@ids)", new { ids = ids.ToArray() }, cancellationToken: cancellationToken))];
    }

    // The names of users, states and sprints by id, for the words a lane's criteria are shown in.
    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(Guid projectId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<BoardNameRow>(new CommandDefinition(
            """
            SELECT id, name FROM app_user WHERE id = ANY(@ids)
            UNION ALL SELECT id, name FROM state WHERE project_id = @projectId AND id = ANY(@ids)
            UNION ALL SELECT id, name FROM sprint WHERE project_id = @projectId AND id = ANY(@ids)
            """,
            new { projectId, ids = ids.ToArray() }, cancellationToken: cancellationToken));
        return rows.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First().Name);
    }

    // The current user's lane preference for the board, or null when they never set one.
    public async Task<string?> LanePreferenceAsync(Guid boardId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var json = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT json::text FROM user_preference WHERE user_id = @user AND key = @key",
            new { user = currentUser.Id, key = LanePreferenceKey(boardId) }, cancellationToken: cancellationToken));
        if (json is null)
        {
            return null;
        }

        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.String ? document.RootElement.GetString() : null;
    }
}
