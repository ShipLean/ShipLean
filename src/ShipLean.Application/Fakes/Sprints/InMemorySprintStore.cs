using System.Data.Common;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Application.Sprints;
using ShipLean.Core;

namespace ShipLean.Application.Fakes.Sprints;

public sealed class InMemorySprintStore(InMemoryPlanning world) : ISprintStore
{
    public Task<SprintDto> CreateAsync(Guid projectId, SprintCreate create, DbTransaction? transaction, CancellationToken cancellationToken = default)
        => Task.FromResult(Open(world, projectId, create));

    // The create rule, callable inside another fake's critical section (the project factory's Sprint 1).
    public static SprintDto Open(InMemoryPlanning world, Guid projectId, SprintCreate create)
    {
        lock (world.Gate)
        {
            world.Project(projectId);
            Check(world, projectId, null, create.Name, create.StartDate, create.EndDate);
            var sprint = new InMemoryPlanning.SprintRow { Id = Guid.NewGuid(), ProjectId = projectId, Name = create.Name.Trim(), Start = create.StartDate, End = create.EndDate, Goal = create.Goal ?? string.Empty };
            world.Sprints.Add(sprint);
            return world.ToDto(sprint);
        }
    }

    public Task<SprintDto> UpdateAsync(Guid id, SprintUpdate update, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            var sprint = world.Sprint(id);
            var name = update.Name ?? sprint.Name;
            var start = update.StartDate ?? sprint.Start;
            var end = update.EndDate ?? sprint.End;
            Check(world, sprint.ProjectId, id, name, start, end);
            sprint.Name = name.Trim();
            sprint.Start = start;
            sprint.End = end;
            sprint.Goal = update.Goal ?? sprint.Goal;
            return Task.FromResult(world.ToDto(sprint));
        }
    }

    public Task<SprintCapacityDto> GetCapacityAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            return Task.FromResult(Capacity(world.Sprint(sprintId)));
        }
    }

    public Task<SprintCapacityDto> PutCapacityAsync(Guid sprintId, SprintCapacityPut capacities, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            var sprint = world.Sprint(sprintId);
            var project = world.Project(sprint.ProjectId);
            foreach (var row in capacities.Members)
            {
                if (row.Points < 0 || !project.Members.ContainsKey(row.UserId))
                {
                    throw ApiException.Validation("Capacity is zero or more points for a member of the project.");
                }
            }
            foreach (var left in sprint.Capacity.Keys.Except(capacities.Members.Select(m => m.UserId)).ToList())
            {
                sprint.Capacity[left] = (sprint.Capacity[left].Points, sprint.Capacity[left].Note, true);
            }
            foreach (var row in capacities.Members)
            {
                sprint.Capacity[row.UserId] = (row.Points, row.Note, false);
            }
            return Task.FromResult(Capacity(sprint));
        }
    }

    public Task<RolloverDto> GetRolloverAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            var sprint = world.Sprint(sprintId);
            var next = world.Sprints.Where(s => s.ProjectId == sprint.ProjectId && s.Start > sprint.End).OrderBy(s => s.Start).FirstOrDefault();
            var items = world.CountedItems(sprintId).Where(i => i.CompletedOn is null && world.States.Single(s => s.Id == i.StateId).Category != StateCategory.Removed)
                .Select(i => ToRolloverItem(i, next?.Id)).ToList();
            var status = sprint.Rollover ?? Contracts.Sprints.RolloverStatus.Pending;
            return Task.FromResult(new RolloverDto(
                Ref(sprint), status, items, next is null ? null : Ref(next), world.TeamCapacity(next), next is null ? 0 : world.CountedItems(next.Id).Sum(i => i.Points ?? 0)));
        }
    }

    public Task<SprintDto> RolloverAsync(Guid sprintId, RolloverRequest request, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            var sprint = world.Sprint(sprintId);
            if (sprint.End > world.Today)
            {
                throw ApiException.Validation("The sprint has not ended.");
            }
            foreach (var move in request.Moves)
            {
                var item = world.Items.FirstOrDefault(i => i.Id == move.WorkItemId) ?? throw ApiException.NotFound("There is no such work item.");
                if (item.SprintId != sprintId)
                {
                    throw ApiException.Validation("That item is not in the sprint.");
                }
                if (move.TargetSprintId is { } target && world.Sprint(target).ProjectId != sprint.ProjectId)
                {
                    throw ApiException.Validation("The target sprint belongs to another project.");
                }
            }
            foreach (var move in request.Moves)
            {
                world.MoveItem(move.WorkItemId, move.TargetSprintId, world.Today);
            }
            sprint.Rollover = Contracts.Sprints.RolloverStatus.Decided;
            return Task.FromResult(world.ToDto(sprint));
        }
    }

    private static void Check(InMemoryPlanning world, Guid projectId, Guid? self, string name, DateOnly start, DateOnly end)
    {
        if (string.IsNullOrWhiteSpace(name) || end < start)
        {
            throw ApiException.Validation("A sprint needs a name and an end on or after its start.");
        }
        var others = world.Sprints.Where(s => s.ProjectId == projectId && s.Id != self).ToList();
        if (others.Any(s => s.Start <= end && start <= s.End))
        {
            throw ApiException.Validation("Sprints cannot overlap.");
        }
        if (others.Any(s => string.Equals(s.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw ApiException.Conflict("A sprint with that name exists.");
        }
    }

    private RolloverItemDto ToRolloverItem(InMemoryPlanning.ItemRow item, Guid? targetSprintId)
    {
        var state = world.States.Single(s => s.Id == item.StateId);
        var tasks = world.Items.Where(t => t.ParentId == item.Id && t.Type == WorkItemTypeId.Task).ToList();
        return new RolloverItemDto(
            item.Id, item.Key, item.Title, item.Type, new StateRef(state.Id, state.Name, state.Category),
            item.AssigneeId is { } assignee ? world.Users[assignee].User : null, item.Points,
            tasks.Count(t => world.States.Single(s => s.Id == t.StateId).Category is not (StateCategory.Completed or StateCategory.Removed)), tasks.Count, targetSprintId);
    }

    private SprintCapacityDto Capacity(InMemoryPlanning.SprintRow sprint)
    {
        var members = sprint.Capacity.Where(c => !c.Value.Archived).Select(c => new MemberCapacityDto(world.Users[c.Key].User, c.Value.Points, c.Value.Note)).ToList();
        return new SprintCapacityDto(sprint.Id, members, members.Sum(m => m.Points));
    }

    private SprintRef Ref(InMemoryPlanning.SprintRow sprint) => new(sprint.Id, sprint.Name, SprintLabels.Phase(sprint.Start, sprint.End, world.Today).ToStatus());
}
