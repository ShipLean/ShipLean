using System.Data.Common;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Boards.Settings;

// IBoardSettingsStore over board.settings. A put holds the board row FOR UPDATE, so two puts on one board serialise and a
// card move that re-checks its column under FOR SHARE cannot land in a column this put is removing.
public sealed class BoardSettingsStore(NpgsqlDataSource dataSource, TimeProvider clock, ICriteriaEvaluator criteria, IProjectEvents events) : IBoardSettingsStore
{
    private const string SelectBoard = "SELECT id, project_id AS projectId, level, settings::text AS settings, updated_at AS updatedAt FROM board WHERE id = @boardId";

    public async Task<BoardSettingsDto> GetAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var board = await FindAsync(connection, null, boardId, forUpdate: false, cancellationToken);
        return await ToDtoAsync(connection, null, board, cancellationToken);
    }

    public async Task<BoardSettingsPutResult> PutAsync(Guid boardId, BoardSettingsPut put, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var board = await FindAsync(connection, transaction, boardId, forUpdate: true, cancellationToken);
        var current = Read(board.Settings);
        var (merged, mergedSections) = BoardSections.Merge(current, put.BasedOn, put.Settings);
        var states = await LiveStatesAsync(connection, transaction, board.ProjectId, cancellationToken);
        var level = ParseLevel(board.Level);
        BoardSettingsValidation.Check(level, merged, states, criteria, current.Transitions);
        await CheckConnectionsAsync(connection, transaction, merged, current, cancellationToken);
        merged = merged with { Transitions = TransitionStates.Normalize(merged.Transitions, await AllStatesAsync(connection, transaction, board.ProjectId, cancellationToken)) };
        await CheckRemovedColumnsAsync(connection, transaction, board, level, current, merged, cancellationToken);
        var now = clock.GetUtcNow();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE board SET settings = @json::jsonb, updated_at = @now WHERE id = @id",
            new { id = boardId, json = JsonSerializer.Serialize(merged, PersistedJson.Options), now }, transaction, cancellationToken: cancellationToken));
        var saved = await ToDtoAsync(connection, transaction, board with { Settings = JsonSerializer.Serialize(merged, PersistedJson.Options), UpdatedAt = now }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var changed = BoardSections.Changed(current, merged);
        if (changed.Count > 0)
        {
            await events.PublishAsync(new BoardSettingsChanged(board.ProjectId, boardId, changed));
        }
        return new BoardSettingsPutResult(saved, mergedSections);
    }

    public BoardSettingsJson BuildDefaults(BoardLevel level, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType)
        => BoardDefaultSettings.Build(level, statesByType);

    public async Task InsertAsync(Guid projectId, BoardLevel level, BoardSettingsJson settings, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var inserted = await transaction.Connection!.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            INSERT INTO board (id, project_id, level, settings, created_at, updated_at)
            VALUES (@id, @projectId, @level, @json::jsonb, @now, @now)
            ON CONFLICT (project_id, level) DO NOTHING
            RETURNING id
            """,
            new { id = Guid.CreateVersion7(), projectId, level = LevelText(level), json = JsonSerializer.Serialize(settings, PersistedJson.Options), now },
            transaction, cancellationToken: cancellationToken));
        if (inserted is null)
        {
            throw ApiException.Conflict($"The project already has a {level} board.");
        }
    }

    public async Task<IReadOnlyList<BoardSummaryDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(Guid Id, string Level)>(new CommandDefinition(
            """
            SELECT id, level FROM board WHERE project_id = @projectId
            ORDER BY array_position(ARRAY['stories', 'features', 'epics', 'taskboard'], level)
            """,
            new { projectId }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new BoardSummaryDto(r.Id, ParseLevel(r.Level), ParseLevel(r.Level).ToString()))];
    }

    private static async Task<BoardRow> FindAsync(DbConnection connection, DbTransaction? transaction, Guid boardId, bool forUpdate, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<BoardRow>(new CommandDefinition(
            forUpdate ? SelectBoard + " FOR UPDATE" : SelectBoard,
            new { boardId }, transaction, cancellationToken: cancellationToken));
        return row ?? throw ApiException.NotFound("Board not found.");
    }

    // The project's live (not archived) states by type, Removed included, in state order.
    private static async Task<Dictionary<WorkItemTypeId, List<StateRef>>> LiveStatesAsync(
        DbConnection connection, DbTransaction? transaction, Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<StateRow>(new CommandDefinition(
            "SELECT id, type, name, category FROM state WHERE project_id = @projectId AND archived_at IS NULL ORDER BY sort_order, name",
            new { projectId }, transaction, cancellationToken: cancellationToken));
        return rows
            .GroupBy(r => Enum.Parse<WorkItemTypeId>(r.Type, true))
            .ToDictionary(g => g.Key, g => g.Select(r => new StateRef(r.Id, r.Name, Enum.Parse<StateCategory>(r.Category, true))).ToList());
    }

    // Every state of the project, archived ones too: what TransitionStates.Normalize reads.
    private static async Task<List<TransitionState>> AllStatesAsync(DbConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(Guid Id, string Type, string Name, bool Archived)>(new CommandDefinition(
            "SELECT id, type, name, archived_at IS NOT NULL AS archived FROM state WHERE project_id = @projectId",
            new { projectId }, transaction, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new TransitionState(r.Id, Enum.Parse<WorkItemTypeId>(r.Type, true), r.Name, r.Archived))];
    }

    // A connection named by an action must exist and not be archived, unless the action already saved it.
    private static async Task CheckConnectionsAsync(DbConnection connection, DbTransaction transaction, BoardSettingsJson merged, BoardSettingsJson current, CancellationToken cancellationToken)
    {
        var saved = current.Transitions.SelectMany(t => t.Actions).Where(a => a.ConnectionId is not null).Select(a => (a.Id, a.ConnectionId)).ToHashSet();
        var ids = merged.Transitions.SelectMany(t => t.Actions)
            .Where(a => a.ConnectionId is not null && !saved.Contains((a.Id, a.ConnectionId)))
            .Select(a => a.ConnectionId!.Value).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }
        var live = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM agent_connection WHERE id = ANY(@ids) AND archived_at IS NULL", new { ids }, transaction, cancellationToken: cancellationToken));
        if (live != ids.Length)
        {
            throw ApiException.Validation("Pick an agent connection that is not archived.");
        }
    }

    private static async Task CheckRemovedColumnsAsync(
        DbConnection connection, DbTransaction transaction, BoardRow board, BoardLevel level, BoardSettingsJson current, BoardSettingsJson merged, CancellationToken cancellationToken)
    {
        var kept = merged.Columns.Select(c => c.Id).ToHashSet();
        var removed = current.Columns.Where(c => !kept.Contains(c.Id)).ToList();
        if (removed.Count == 0)
        {
            return;
        }
        var types = BoardDefaultSettings.TypesShown(level).Select(t => t.ToString().ToLowerInvariant()).ToArray();
        var occupied = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT DISTINCT board_column_id FROM work_item WHERE project_id = @projectId AND type = ANY(@types) AND board_column_id = ANY(@ids)",
            new { projectId = board.ProjectId, types, ids = removed.Select(c => c.Id).ToArray() }, transaction, cancellationToken: cancellationToken));
        var held = occupied.ToHashSet();
        var first = removed.FirstOrDefault(c => held.Contains(c.Id));
        if (first is not null)
        {
            throw ApiException.Validation($"Column {first.Name} still has cards. Move them first.");
        }
    }

    private static async Task<BoardSettingsDto> ToDtoAsync(DbConnection connection, DbTransaction? transaction, BoardRow board, CancellationToken cancellationToken)
    {
        var level = ParseLevel(board.Level);
        var settings = Read(board.Settings);
        var byType = await LiveStatesAsync(connection, transaction, board.ProjectId, cancellationToken);
        var states = BoardDefaultSettings.TypesShown(level)
            .Where(byType.ContainsKey)
            .Select(t => new BoardTypeStatesDto(t, byType[t]))
            .ToList();
        return new BoardSettingsDto(board.Id, board.ProjectId, level, settings, board.UpdatedAt, states, Warnings(level, settings, states));
    }

    // Level boards only: the Taskboard's columns are the Task states themselves, so none can be unmapped.
    private static List<BoardWarningDto> Warnings(BoardLevel level, BoardSettingsJson settings, List<BoardTypeStatesDto> states)
    {
        if (level == BoardLevel.Taskboard)
        {
            return [];
        }
        var mapped = settings.Columns.SelectMany(c => c.StateMappings).Select(m => m.StateId).ToHashSet();
        return states
            .SelectMany(t => t.States.Where(s => s.Category != StateCategory.Removed && !mapped.Contains(s.Id)).Select(s => (t.Type, State: s)))
            .Select(x => new BoardWarningDto(BoardWarningKind.UnmappedState, x.Type, x.State, $"{x.State.Name} ({x.Type}) is not on any column."))
            .ToList();
    }

    private static BoardSettingsJson Read(string json)
        => JsonSerializer.Deserialize<BoardSettingsJson>(json, PersistedJson.Options)!;

    private static BoardLevel ParseLevel(string text) => Enum.Parse<BoardLevel>(text, ignoreCase: true);

    private static string LevelText(BoardLevel level) => level.ToString().ToLowerInvariant();
}
