using ShipLean.Application.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Fakes.Projects;

// The projects, members, states, sprints and work items the four planning fakes share. One lock guards everything; no I/O,
// no events, nothing removed (a state is renamed or its items move; a sprint is never dropped). The Add* methods are the
// arrange API the contract fixtures' hooks call.
public sealed class InMemoryPlanning(TimeProvider? clock = null)
{
    internal sealed class ProjectRow
    {
        public required Guid Id { get; init; }
        public required string Key { get; init; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        internal List<int> WorkingDays { get; set; } = [1, 2, 3, 4, 5];
        public int VelocityWindow { get; set; } = 3;
        public SprintNamingConvention SprintNaming { get; set; } = SprintNamingConvention.Number;
        public decimal? VelocityOverride { get; set; }
        public int NextItemNumber { get; set; } = 1;
        internal Dictionary<Guid, ProjectRole> Members { get; } = [];
    }

    internal sealed class StateRow
    {
        public required Guid Id { get; init; }
        public required Guid ProjectId { get; init; }
        public required WorkItemTypeId Type { get; init; }
        public required string Name { get; set; }
        public required StateCategory Category { get; set; }
        public int Order { get; set; }
        public bool Archived { get; set; }
    }

    internal sealed class SprintRow
    {
        public required Guid Id { get; init; }
        public required Guid ProjectId { get; init; }
        public required string Name { get; set; }
        public DateOnly Start { get; set; }
        public DateOnly End { get; set; }
        public string Goal { get; set; } = string.Empty;
        public RolloverStatus? Rollover { get; set; }
        internal Dictionary<Guid, (decimal Points, string? Note, bool Archived)> Capacity { get; } = [];
    }

    internal sealed class ItemRow
    {
        public required Guid Id { get; init; }
        public required Guid ProjectId { get; init; }
        public required string Key { get; init; }
        public required WorkItemTypeId Type { get; init; }
        public decimal? Points { get; set; }
        public Guid? SprintId { get; set; }
        public required Guid StateId { get; set; }
        public string Title { get; init; } = string.Empty;
        public Guid? AssigneeId { get; set; }
        public Guid? ParentId { get; init; }
        public DateOnly? CompletedOn { get; set; }

        // Which sprint the item sat in from each day on, oldest first; SprintId is the latest entry.
        internal List<(DateOnly From, Guid? SprintId)> Moves { get; } = [];

        // Which state category the item was in from each day on, oldest first (empty until it first changes state).
        internal List<(DateOnly From, StateCategory Category)> Categories { get; } = [];
    }

    internal object Gate { get; } = new();

    internal TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    internal List<ProjectRow> Projects { get; } = [];

    internal List<StateRow> States { get; } = [];

    internal List<SprintRow> Sprints { get; } = [];

    internal List<ItemRow> Items { get; } = [];

    internal Dictionary<Guid, (UserRef User, string Email)> Users { get; } = [];

    internal DateOnly Today => SprintLabels.Today(Clock);

    public Guid AddUser(string name, string email)
    {
        lock (Gate)
        {
            var id = Guid.NewGuid();
            var initials = new string([.. name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0]))]);
            Users[id] = (new UserRef(id, name, initials, "#5b6b8c", null), email);
            return id;
        }
    }

    public Guid AddProject(string key, string name, bool withDefaultStates = true)
    {
        lock (Gate)
        {
            var project = new ProjectRow { Id = Guid.NewGuid(), Key = key, Name = name };
            Projects.Add(project);
            if (withDefaultStates)
            {
                this.AddDefaultStates(project.Id);
            }
            return project.Id;
        }
    }

    public void AddMember(Guid projectId, Guid userId, ProjectRole role)
    {
        lock (Gate)
        {
            this.Project(projectId).Members[userId] = role;
        }
    }

    public Guid AddSprint(Guid projectId, string name, DateOnly start, DateOnly end)
    {
        lock (Gate)
        {
            var sprint = new SprintRow { Id = Guid.NewGuid(), ProjectId = projectId, Name = name, Start = start, End = end };
            Sprints.Add(sprint);
            return sprint.Id;
        }
    }

    // A work item in its type's first Proposed state, or its first Completed state when completedOn is given. In a sprint
    // it is there from the day before the sprint starts (committed), or from its first day (addedAfterStart: added, not committed). Backlog order is creation order.
    public Guid AddItem(
        Guid projectId, WorkItemTypeId type, decimal? points, Guid? sprintId = null, bool addedAfterStart = false, DateOnly? completedOn = null,
        string? title = null, Guid? assigneeId = null, Guid? parentId = null)
    {
        lock (Gate)
        {
            var project = this.Project(projectId);
            var category = completedOn is null ? StateCategory.Proposed : StateCategory.Completed;
            var item = new ItemRow
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = $"{project.Key}-{project.NextItemNumber++}", Type = type, Points = points,
                SprintId = sprintId, StateId = States.First(s => s.ProjectId == projectId && !s.Archived && s.Type == type && s.Category == category).Id,
                Title = title ?? $"{project.Key}-{project.NextItemNumber - 1} title", AssigneeId = assigneeId, ParentId = parentId, CompletedOn = completedOn,
            };
            if (completedOn is { } done)
            {
                item.Categories.Add((done, StateCategory.Completed));
            }
            if (sprintId is { } joined)
            {
                var sprint = this.Sprint(joined);
                item.Moves.Add((addedAfterStart ? sprint.Start : sprint.Start.AddDays(-1), joined));
            }
            Items.Add(item);
            return item.Id;
        }
    }

    // An open work item (a story) sitting in the given state.
    public Guid AddItemInState(Guid projectId, Guid stateId)
    {
        lock (Gate)
        {
            var id = AddItem(projectId, WorkItemTypeId.Story, null);
            Items.Single(i => i.Id == id).StateId = stateId;
            return id;
        }
    }

    // The item joins the sprint (or the backlog when null) from that day on.
    public void MoveItem(Guid itemId, Guid? toSprintId, DateOnly on)
    {
        lock (Gate)
        {
            if (toSprintId is { } target)
            {
                this.Sprint(target);
            }
            var item = Items.Single(i => i.Id == itemId);
            item.Moves.Add((on, toSprintId));
            item.SprintId = toSprintId;
        }
    }

    // The item reaches its type's first Completed state on that day.
    public void CompleteItem(Guid itemId, DateOnly on)
    {
        lock (Gate)
        {
            var item = Items.Single(i => i.Id == itemId);
            item.CompletedOn = on;
            Enter(item, on, StateCategory.Completed);
        }
    }

    // The item goes back to its type's first InProgress state on that day (it is no longer completed).
    public void ReopenItem(Guid itemId, DateOnly on)
    {
        lock (Gate)
        {
            var item = Items.Single(i => i.Id == itemId);
            item.CompletedOn = null;
            Enter(item, on, StateCategory.InProgress);
        }
    }

    // The item reaches its type's Removed state on that day.
    public void RemoveItem(Guid itemId, DateOnly on)
    {
        lock (Gate)
        {
            Enter(Items.Single(i => i.Id == itemId), on, StateCategory.Removed);
        }
    }

    private void Enter(ItemRow item, DateOnly on, StateCategory category)
    {
        item.Categories.Add((on, category));
        item.StateId = States.First(s => s.ProjectId == item.ProjectId && !s.Archived && s.Type == item.Type && s.Category == category).Id;
    }

    // A member who enters this many points for the sprint (a new user and member of the project).
    public void SetTeamCapacity(Guid sprintId, decimal points)
    {
        lock (Gate)
        {
            var sprint = this.Sprint(sprintId);
            var user = AddUser($"Member {Users.Count + 1}", $"member{Users.Count + 1}@example.test");
            this.Project(sprint.ProjectId).Members[user] = ProjectRole.Member;
            sprint.Capacity[user] = (points, null, false);
        }
    }

    public Guid ItemState(Guid itemId)
    {
        lock (Gate)
        {
            return Items.Single(i => i.Id == itemId).StateId;
        }
    }

    public IReadOnlyList<(Guid UserId, ProjectRole Role)> MembersOf(Guid projectId)
    {
        lock (Gate)
        {
            return [.. this.Project(projectId).Members.Select(m => (m.Key, m.Value))];
        }
    }

    public void AddState(Guid projectId, WorkItemTypeId type, string name, StateCategory category)
    {
        lock (Gate)
        {
            States.Add(new StateRow { Id = Guid.NewGuid(), ProjectId = projectId, Type = type, Name = name, Category = category, Order = States.Count(s => s.ProjectId == projectId && s.Type == type) + 1 });
        }
    }

    public IReadOnlyList<(WorkItemTypeId Type, string Name)> StateNames(Guid projectId)
    {
        lock (Gate)
        {
            return [.. States.Where(s => s.ProjectId == projectId && !s.Archived).Select(s => (s.Type, s.Name))];
        }
    }

    public int ItemCount(Guid projectId)
    {
        lock (Gate)
        {
            return Items.Count(i => i.ProjectId == projectId);
        }
    }

    public int SprintCount(Guid projectId)
    {
        lock (Gate)
        {
            return Sprints.Count(s => s.ProjectId == projectId);
        }
    }

    public void SetPoints(Guid itemId, decimal? points)
    {
        lock (Gate)
        {
            Items.Single(i => i.Id == itemId).Points = points;
        }
    }

    public void SetVelocityOverride(Guid projectId, decimal? value)
    {
        lock (Gate)
        {
            this.Project(projectId).VelocityOverride = value;
        }
    }

    public void SetSprintNaming(Guid projectId, SprintNamingConvention naming)
    {
        lock (Gate)
        {
            this.Project(projectId).SprintNaming = naming;
        }
    }
}
