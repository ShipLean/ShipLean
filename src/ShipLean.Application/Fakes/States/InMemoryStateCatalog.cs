using System.Data.Common;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Fakes.States;

public sealed class InMemoryStateCatalog(InMemoryPlanning world) : IStateCatalog
{
    private static readonly WorkItemTypeId[] TypeOrder = [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task];

    public Task<IReadOnlyList<StateDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            world.Project(projectId);
            IReadOnlyList<StateDto> list = [.. world.States.Where(s => s.ProjectId == projectId && !s.Archived)
                .OrderBy(s => Array.IndexOf(TypeOrder, s.Type)).ThenBy(s => s.Order)
                .Select(s => new StateDto(s.Id, s.Type, s.Name, s.Category, s.Order, world.Items.Count(i => i.StateId == s.Id)))];
            return Task.FromResult(list);
        }
    }

    public Task CreateDefaultsAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            world.Project(projectId);
            if (world.States.All(s => s.ProjectId != projectId))
            {
                world.AddDefaultStates(projectId);
            }
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyDictionary<Guid, Guid>> CopyAsync(Guid fromProjectId, Guid toProjectId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            world.Project(fromProjectId);
            world.Project(toProjectId);
            var map = new Dictionary<Guid, Guid>();
            foreach (var state in world.States.Where(s => s.ProjectId == fromProjectId && !s.Archived).ToList())
            {
                var copy = new InMemoryPlanning.StateRow { Id = Guid.NewGuid(), ProjectId = toProjectId, Type = state.Type, Name = state.Name, Category = state.Category, Order = state.Order };
                world.States.Add(copy);
                map[state.Id] = copy.Id;
            }
            return Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(map);
        }
    }

    public Task ApplyAsync(Guid projectId, StateChanges changes, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            world.Project(projectId);
            var live = world.States.Where(s => s.ProjectId == projectId && !s.Archived).ToList();
            var added = new List<InMemoryPlanning.StateRow>();
            foreach (var add in changes.Add)
            {
                if (string.IsNullOrWhiteSpace(add.Name) || world.States.Any(s => s.Id == add.Id) || added.Any(a => a.Id == add.Id))
                {
                    throw ApiException.Validation("A state needs a name and an id that is not used yet.");
                }
                var order = live.Concat(added).Where(s => s.Type == add.Type).Select(s => s.Order).DefaultIfEmpty(0).Max() + 1;
                added.Add(new InMemoryPlanning.StateRow { Id = add.Id, ProjectId = projectId, Type = add.Type, Name = add.Name.Trim(), Category = add.Category, Order = order });
            }
            var candidates = live.Concat(added).ToList();
            var touched = new HashSet<WorkItemTypeId>(added.Select(a => a.Type));
            var updates = new List<(InMemoryPlanning.StateRow Row, string Name, StateCategory Category)>();
            foreach (var update in changes.Update)
            {
                if (string.IsNullOrWhiteSpace(update.Name))
                {
                    throw ApiException.Validation("A state needs a name.");
                }
                var row = StateRow(candidates, [update.StateId]);
                touched.Add(row.Type);
                updates.Add((row, update.Name.Trim(), update.Category));
            }
            foreach (var reorder in changes.Reorder)
            {
                touched.Add(reorder.Type);
                foreach (var id in reorder.StateIds)
                {
                    if (StateRow(candidates, [id]).Type != reorder.Type)
                    {
                        throw ApiException.Validation("A reorder lists the states of one type.");
                    }
                }
            }
            var removed = changes.Remove.Select(r => StateRow(candidates, [r.StateId])).ToList();
            foreach (var remove in changes.Remove)
            {
                var state = StateRow(candidates, [remove.StateId]);
                var target = StateRow(candidates, [remove.TargetStateId]);
                if (target.Type != state.Type || target.Id == state.Id || changes.Remove.Any(r => r.StateId == target.Id))
                {
                    throw ApiException.Validation("Items must move to another state of the same type.");
                }
                touched.Add(state.Type);
            }

            var finalStates = candidates.Where(s => !removed.Contains(s))
                .Select(s => (Row: s, Name: s.Name, Category: s.Category))
                .Select(s => updates.Any(u => u.Row == s.Row) ? (s.Row, updates.Last(u => u.Row == s.Row).Name, updates.Last(u => u.Row == s.Row).Category) : s)
                .ToList();
            foreach (var type in touched)
            {
                var ofType = finalStates.Where(s => s.Row.Type == type).ToList();
                if (ofType.Select(s => s.Name.ToUpperInvariant()).Distinct().Count() != ofType.Count)
                {
                    throw ApiException.Validation("A state needs a name that is not already used by its type.");
                }
                var missing = new[] { StateCategory.Proposed, StateCategory.Completed }.FirstOrDefault(c => ofType.All(s => s.Category != c), StateCategory.Removed);
                if (missing != StateCategory.Removed)
                {
                    throw ApiException.Validation($"A type needs at least one {missing} state.");
                }
            }

            foreach (var row in added)
            {
                world.States.Add(row);
            }
            foreach (var (row, name, category) in updates)
            {
                row.Name = name;
                row.Category = category;
            }
            foreach (var reorder in changes.Reorder)
            {
                var position = 1;
                foreach (var id in reorder.StateIds)
                {
                    StateRow(candidates, [id]).Order = position++;
                }
            }
            foreach (var remove in changes.Remove)
            {
                foreach (var item in world.Items.Where(i => i.StateId == remove.StateId))
                {
                    item.StateId = remove.TargetStateId;
                }
                StateRow(candidates, [remove.StateId]).Archived = true;
            }
            return Task.CompletedTask;
        }
    }

    private static InMemoryPlanning.StateRow StateRow(List<InMemoryPlanning.StateRow> live, IEnumerable<Guid> ids)
    {
        InMemoryPlanning.StateRow? last = null;
        foreach (var id in ids)
        {
            last = live.FirstOrDefault(s => s.Id == id) ?? throw ApiException.NotFound("There is no such state.");
        }
        return last!;
    }
}
