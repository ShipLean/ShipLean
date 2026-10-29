using System.Data.Common;
using ShipLean.Application.Features.Boards.Settings;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Fakes.Boards;

// IBoardSettingsStore in memory: thread-safe, no I/O, publishes no events. It does not know a project's states or its cards
// unless told: AddProject gives a project its states, MarkColumnHoldsCards says a column has cards.
public sealed class InMemoryBoardSettingsStore(TimeProvider time, ICriteriaEvaluator criteria) : IBoardSettingsStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, InMemoryBoard> _boards = [];
    private readonly Dictionary<Guid, Dictionary<WorkItemTypeId, List<StateRef>>> _states = [];
    private readonly HashSet<(Guid BoardId, string ColumnId)> _columnsWithCards = [];
    private readonly Dictionary<Guid, bool> _connections = [];
    private readonly List<(Guid ProjectId, TransitionState State)> _archived = [];

    public void AddProject(Guid projectId, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType)
    {
        lock (_gate)
        {
            _states[projectId] = statesByType.ToDictionary(p => p.Key, p => p.Value.ToList());
        }
    }

    public void AddState(Guid projectId, WorkItemTypeId type, StateRef state)
    {
        lock (_gate)
        {
            var byType = _states.GetValueOrDefault(projectId) ?? throw new InvalidOperationException("Unknown project.");
            byType[type] = [.. byType.GetValueOrDefault(type) ?? [], state];
        }
    }

    // Archives a state the way the state catalog does: it is no longer live, but a stored transition may still name it.
    public void ArchiveState(Guid projectId, WorkItemTypeId type, Guid stateId)
    {
        lock (_gate)
        {
            var byType = _states.GetValueOrDefault(projectId) ?? throw new InvalidOperationException("Unknown project.");
            var state = byType[type].Single(s => s.Id == stateId);
            byType[type] = [.. byType[type].Where(s => s.Id != stateId)];
            _archived.Add((projectId, new TransitionState(state.Id, type, state.Name, true)));
        }
    }

    // Says an agent connection exists (archived or not); a transition action can name only the live ones it has not saved already.
    public void AddConnection(Guid id, bool archived = false)
    {
        lock (_gate)
        {
            _connections[id] = archived;
        }
    }

    public void MarkColumnHoldsCards(Guid boardId, string columnId)
    {
        lock (_gate)
        {
            _columnsWithCards.Add((boardId, columnId));
        }
    }

    public Task<BoardSettingsDto> GetAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(ToDto(Find(boardId)));
        }
    }

    public Task<BoardSettingsPutResult> PutAsync(Guid boardId, BoardSettingsPut put, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var board = Find(boardId);
            var (merged, mergedSections) = BoardSections.Merge(board.Settings, put.BasedOn, put.Settings);
            BoardSettingsValidation.Check(board.Level, merged, _states.GetValueOrDefault(board.ProjectId) ?? [], criteria, board.Settings.Transitions);
            CheckConnections(merged, board.Settings);
            merged = merged with { Transitions = TransitionStates.Normalize(merged.Transitions, AllStates(board.ProjectId)) };
            var kept = merged.Columns.Select(c => c.Id).ToHashSet();
            var lost = board.Settings.Columns.Where(c => !kept.Contains(c.Id) && _columnsWithCards.Contains((boardId, c.Id))).ToList();
            if (lost.Count > 0)
            {
                throw ApiException.Validation($"Column {lost[0].Name} still has cards. Move them first.");
            }
            var saved = board with { Settings = merged, UpdatedAt = time.GetUtcNow() };
            _boards[boardId] = saved;
            return Task.FromResult(new BoardSettingsPutResult(ToDto(saved), mergedSections));
        }
    }

    public BoardSettingsJson BuildDefaults(BoardLevel level, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType)
        => BoardDefaultSettings.Build(level, statesByType);

    public Task InsertAsync(Guid projectId, BoardLevel level, BoardSettingsJson settings, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_boards.Values.Any(b => b.ProjectId == projectId && b.Level == level))
            {
                throw ApiException.Conflict($"The project already has a {level} board.");
            }
            var id = Guid.NewGuid();
            _boards[id] = new InMemoryBoard(id, projectId, level, settings, time.GetUtcNow());
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<BoardSummaryDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<BoardSummaryDto> boards = _boards.Values
                .Where(b => b.ProjectId == projectId)
                .OrderBy(b => b.Level)
                .Select(b => new BoardSummaryDto(b.Id, b.Level, b.Level.ToString()))
                .ToList();
            return Task.FromResult(boards);
        }
    }

    private List<TransitionState> AllStates(Guid projectId)
        => [.. (_states.GetValueOrDefault(projectId) ?? []).SelectMany(p => p.Value.Select(s => new TransitionState(s.Id, p.Key, s.Name, false))),
            .. _archived.Where(a => a.ProjectId == projectId).Select(a => a.State)];

    private void CheckConnections(BoardSettingsJson merged, BoardSettingsJson current)
    {
        var saved = current.Transitions.SelectMany(t => t.Actions).Where(a => a.ConnectionId is not null).Select(a => (a.Id, a.ConnectionId)).ToHashSet();
        var unusable = merged.Transitions.SelectMany(t => t.Actions)
            .Where(a => a.ConnectionId is { } id && !saved.Contains((a.Id, a.ConnectionId)) && (!_connections.TryGetValue(id, out var archived) || archived));
        if (unusable.Any())
        {
            throw ApiException.Validation("Pick an agent connection that is not archived.");
        }
    }

    private InMemoryBoard Find(Guid boardId)
        => _boards.GetValueOrDefault(boardId) ?? throw ApiException.NotFound("Board not found.");

    private BoardSettingsDto ToDto(InMemoryBoard board)
    {
        var byType = _states.GetValueOrDefault(board.ProjectId) ?? [];
        var shown = BoardDefaultSettings.TypesShown(board.Level);
        var states = shown.Where(byType.ContainsKey).Select(t => new BoardTypeStatesDto(t, byType[t])).ToList();
        return new BoardSettingsDto(board.Id, board.ProjectId, board.Level, board.Settings, board.UpdatedAt, states, Warnings(board, states));
    }

    // Level boards only: the Taskboard's columns are the Task states themselves, so none can be unmapped.
    private static List<BoardWarningDto> Warnings(InMemoryBoard board, List<BoardTypeStatesDto> states)
    {
        if (board.Level == BoardLevel.Taskboard)
        {
            return [];
        }
        var mapped = board.Settings.Columns.SelectMany(c => c.StateMappings).Select(m => m.StateId).ToHashSet();
        return states
            .SelectMany(t => t.States.Where(s => s.Category != StateCategory.Removed && !mapped.Contains(s.Id)).Select(s => (t.Type, State: s)))
            .Select(x => new BoardWarningDto(BoardWarningKind.UnmappedState, x.Type, x.State, $"{x.State.Name} ({x.Type}) is not on any column."))
            .ToList();
    }
}
