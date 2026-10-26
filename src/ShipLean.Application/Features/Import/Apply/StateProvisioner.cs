using Npgsql;
using ShipLean.Application.Features.Import.Mapping;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Import.Apply;

// Finds the state a Jira status lands in, creating it when the project has none by that name. A state created here gets a board column in
// the same step (a column named like the state, placed after the last column whose state of that type is in the same or an earlier category),
// or joins the column of that name that another type's new state made: without a column the item would be on no board.
// The Taskboard has no columns, so a Task state needs nothing more.
public sealed class StateProvisioner(NpgsqlDataSource dataSource, IStateCatalog states, IBoardSettingsStore boards, IProjectEvents events)
{
    public async Task<Guid> EnsureAsync(ImportRun run, WorkItemTypeId type, string name, StateCategory category, CancellationToken cancellationToken)
    {
        if (run.States.TryGetValue((type.ToString(), name.ToLowerInvariant()), out var known))
        {
            return known;
        }

        var live = await states.ListAsync(run.ProjectId, cancellationToken);
        var found = live.FirstOrDefault(s => s.Type == type && s.Category != StateCategory.Removed && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        var id = found?.Id ?? await CreateAsync(run.ProjectId, type, name, category, live, cancellationToken);
        if (found is not null)
        {
            // A state an earlier run made may still have no column (its column add failed): the re-run repairs it.
            await EnsureColumnAsync(run.ProjectId, type, id, name, found.Category, live, cancellationToken);
        }

        run.States[(type.ToString(), name.ToLowerInvariant())] = id;
        return id;
    }

    private async Task<Guid> CreateAsync(Guid projectId, WorkItemTypeId type, string name, StateCategory category, IReadOnlyList<StateDto> live, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        var order = live.Where(s => s.Type == type).OrderBy(s => s.Order).ToList();
        var after = order.FindLastIndex(s => s.Category <= category && s.Category != StateCategory.Removed);
        var ids = order.Select(s => s.Id).ToList();
        ids.Insert(after + 1, id);
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await states.ApplyAsync(
                projectId, new StateChanges([new StateAdd(id, type, name, category)], [], [new StateReorder(type, ids)], []), transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await events.PublishAsync(new StatesChanged(projectId));
        await EnsureColumnAsync(projectId, type, id, name, category, live, cancellationToken);
        return id;
    }

    // The state is committed before its column, so a failed column add leaves a state with no column; EnsureAsync calls this for an existing state too.
    // Accepted: the Get and the Put are not one step, so a board save by someone else in those milliseconds (on a new or empty project) is overwritten
    // by this put. Closing the window takes a store method that adds a column under the board's row lock, which is a contract change.
    private async Task EnsureColumnAsync(Guid projectId, WorkItemTypeId type, Guid stateId, string name, StateCategory category, IReadOnlyList<StateDto> live, CancellationToken cancellationToken)
    {
        var level = type switch
        {
            WorkItemTypeId.Epic => BoardLevel.Epics,
            WorkItemTypeId.Feature => BoardLevel.Features,
            WorkItemTypeId.Task => (BoardLevel?)null,
            _ => BoardLevel.Stories,
        };
        if (level is null)
        {
            return;
        }

        var summary = (await boards.ListAsync(projectId, cancellationToken)).Single(b => b.Level == level);
        var settings = (await boards.GetAsync(summary.Id, cancellationToken)).Settings;
        var columns = settings.Columns.ToList();
        if (columns.Any(c => c.StateMappings.Any(m => m.Type == type && m.StateId == stateId)))
        {
            return;
        }

        // A column of that name is joined only when it holds no live state of this type on its own: a column that maps the type to a state
        // no other column maps is somebody's column (renamed, say) and keeps its mapping. A repeated state is a filler another type's new column left.
        var liveIds = live.Where(s => s.Category != StateCategory.Removed).Select(s => s.Id).ToHashSet();
        var sameName = columns.FindIndex(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
            && !c.StateMappings.Any(m => m.Type == type && liveIds.Contains(m.StateId) && columns.Count(o => o.StateMappings.Any(x => x.Type == type && x.StateId == m.StateId)) == 1));
        if (sameName >= 0)
        {
            columns[sameName] = columns[sameName] with { StateMappings = [.. columns[sameName].StateMappings.Select(m => m.Type == type ? m with { StateId = stateId } : m)] };
        }
        else
        {
            var categoryOf = live.ToDictionary(s => s.Id, s => s.Category);
            var after = columns.FindLastIndex(c => c.StateMappings.FirstOrDefault(m => m.Type == type) is { } m && categoryOf.TryGetValue(m.StateId, out var c1) && c1 <= category);
            var neighbour = columns[Math.Max(after, 0)];
            var mappings = neighbour.StateMappings.Select(m => m.Type == type ? m with { StateId = stateId } : m).ToList();
            columns.Insert(after + 1, new BoardColumnJson($"col-{Guid.NewGuid():N}"[..12], name, null, false, string.Empty, mappings));
        }

        await boards.PutAsync(summary.Id, new BoardSettingsPut(settings, settings with { Columns = columns }), cancellationToken);
    }
}
