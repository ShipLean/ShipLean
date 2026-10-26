using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;

namespace ShipLean.Application.Fakes.Projects;

// Lookups and shared calculations over the planning world; callers hold world.Gate.
internal static class PlanningExtensions
{
    public static InMemoryPlanning.ProjectRow Project(this InMemoryPlanning world, Guid id) => world.Projects.FirstOrDefault(p => p.Id == id) ?? throw Errors.NotFound("project");

    public static InMemoryPlanning.SprintRow Sprint(this InMemoryPlanning world, Guid id) => world.Sprints.FirstOrDefault(s => s.Id == id) ?? throw Errors.NotFound("sprint");

    public static SprintDto ToDto(this InMemoryPlanning world, InMemoryPlanning.SprintRow sprint)
        => new(sprint.Id, sprint.Name, sprint.Start, sprint.End, sprint.Goal, SprintLabels.Phase(sprint.Start, sprint.End, world.Today).ToStatus(), sprint.Rollover,
            SprintLabels.Dates(sprint.Start, sprint.End, world.Today), SprintLabels.Time(sprint.Start, sprint.End, world.Today));

    // Only stories and bugs count toward points; task points never do.
    public static bool Counts(this InMemoryPlanning.ItemRow item) => item.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug;

    // Counted items sitting in the sprint now.
    public static IEnumerable<InMemoryPlanning.ItemRow> CountedItems(this InMemoryPlanning world, Guid sprintId)
        => world.Items.Where(i => i.SprintId == sprintId && i.Counts());

    // The sprint the item sat in at the end of that day.
    public static Guid? SprintOn(this InMemoryPlanning.ItemRow item, DateOnly day)
        => item.Moves.OrderBy(m => m.From).LastOrDefault(m => m.From <= day).SprintId;

    // Committed: counted items that were in the sprint at 00:00 on its first day, i.e. at the end of the day before, as they were then (not where they are now).
    public static decimal Committed(this InMemoryPlanning world, InMemoryPlanning.SprintRow sprint)
        => world.Items.Where(i => i.Counts() && i.SprintOn(sprint.Start.AddDays(-1)) == sprint.Id).Sum(i => i.Points ?? 0);

    // The state category the item was in at the end of that day (null before its first state change).
    public static StateCategory? CategoryOn(this InMemoryPlanning.ItemRow item, DateOnly day)
        => item.Categories.Where(c => c.From <= day).OrderBy(c => c.From).Select(c => (StateCategory?)c.Category).LastOrDefault();

    // Completed: counted items that reached Completed between the sprint's first and last day while in it and were still
    // Completed when the sprint ended, or when they left the sprint if that came first.
    public static decimal Completed(this InMemoryPlanning world, InMemoryPlanning.SprintRow sprint)
        => world.Items.Where(i => i.Counts() && i.CompletedIn(sprint)).Sum(i => i.Points ?? 0);

    public static bool CompletedIn(this InMemoryPlanning.ItemRow item, InMemoryPlanning.SprintRow sprint)
        => item.Categories.Any(c =>
        {
            if (c.Category != StateCategory.Completed || c.From < sprint.Start || c.From > sprint.End || item.SprintOn(c.From) != sprint.Id)
            {
                return false;
            }
            var left = item.Moves.Where(m => m.From > c.From && m.SprintId != sprint.Id).OrderBy(m => m.From).Select(m => (DateOnly?)m.From).FirstOrDefault();
            return item.CategoryOn(left is { } day && day <= sprint.End ? day : sprint.End) == StateCategory.Completed;
        });

    // Remaining at the end of a day: counted items in the sprint that day and neither completed nor removed by then.
    public static decimal RemainingOn(this InMemoryPlanning world, InMemoryPlanning.SprintRow sprint, DateOnly day)
        => world.Items.Where(i => i.Counts() && i.SprintOn(day) == sprint.Id && i.CategoryOn(day) is not (StateCategory.Completed or StateCategory.Removed)).Sum(i => i.Points ?? 0);

    // The team's entered capacity for a sprint, or null when nobody has an entry.
    public static decimal? TeamCapacity(this InMemoryPlanning world, InMemoryPlanning.SprintRow? sprint)
    {
        var entered = sprint?.Capacity.Values.Where(c => !c.Archived).ToList();
        return entered is null or { Count: 0 } ? null : entered.Sum(c => c.Points);
    }

    public static VelocityDto Velocity(this InMemoryPlanning world, Guid projectId)
    {
        var project = world.Project(projectId);
        var ended = world.Sprints.Where(s => s.ProjectId == projectId && s.End < world.Today).OrderBy(s => s.Start).TakeLast(project.VelocityWindow).ToList();
        var entries = ended.Select(s => new VelocityEntryDto(s.Id, s.Name, world.Committed(s), world.Completed(s))).ToList();
        var average = entries.Count == 0 ? 0 : Math.Round(entries.Average(e => e.Completed), 2);
        return new VelocityDto(project.VelocityWindow, entries, average, project.VelocityOverride);
    }

    public static void AddDefaultStates(this InMemoryPlanning world, Guid projectId)
    {
        var all = new[] { WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task };
        foreach (var type in all)
        {
            var names = type == WorkItemTypeId.Task
                ? new (string, StateCategory)[] { ("To Do", StateCategory.Proposed), ("In Progress", StateCategory.InProgress), ("In review", StateCategory.Resolved), ("Done", StateCategory.Completed), ("Removed", StateCategory.Removed) }
                : [("New", StateCategory.Proposed), ("Active", StateCategory.InProgress), ("Resolved", StateCategory.Resolved), ("Closed", StateCategory.Completed), ("Removed", StateCategory.Removed)];
            for (var order = 0; order < names.Length; order++)
            {
                world.States.Add(new InMemoryPlanning.StateRow { Id = Guid.NewGuid(), ProjectId = projectId, Type = type, Name = names[order].Item1, Category = names[order].Item2, Order = order + 1 });
            }
        }
    }
}
