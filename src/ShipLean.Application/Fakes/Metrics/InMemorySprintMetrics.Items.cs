using ShipLean.Application.Fakes.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Fakes.Metrics;

public sealed partial class InMemorySprintMetrics
{
    // The same rules as the Postgres metrics, at the fake's one-day grain.
    public Task<IReadOnlyList<SprintItemOutcomeDto>> GetSprintItemsAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            var sprint = world.Sprint(sprintId);
            var lastWorked = LastWorkingDay(world.Project(sprint.ProjectId).WorkingDays, sprint.End);
            var rows = world.Items.Where(i => i.ProjectId == sprint.ProjectId && i.Counts()).Select(item =>
            {
                var committed = item.SprintOn(sprint.Start.AddDays(-1)) == sprint.Id;
                var inAtEnd = item.SprintOn(lastWorked.AddDays(-1)) == sprint.Id || item.SprintOn(sprint.End) == sprint.Id;
                var endState = item.CategoryOn(sprint.End);
                var joined = item.Moves.Any(m => m.SprintId == sprint.Id && m.From >= sprint.Start && m.From <= sprint.End);
                var completed = item.CompletedIn(sprint);
                var added = !committed && joined && inAtEnd && endState != StateCategory.Removed;
                var carried = inAtEnd && endState is not (StateCategory.Completed or StateCategory.Removed) && !completed;
                return new SprintItemOutcomeDto(item.Id, item.Key, item.Title, item.Type, item.Points, committed, added, completed, carried);
            });
            return Task.FromResult<IReadOnlyList<SprintItemOutcomeDto>>([.. rows.Where(r => r.Committed || r.AddedMidSprint || r.Completed || r.CarriedOver).OrderBy(r => r.Key, StringComparer.Ordinal)]);
        }
    }

    // The last working day on or before the sprint's end. A Monday-start sprint ends on Sunday; the rollover is the Friday.
    private static DateOnly LastWorkingDay(IReadOnlyCollection<int> workingDays, DateOnly end)
    {
        var day = end;
        for (var stepped = 0; stepped < 7 && !workingDays.Contains(day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek); stepped++)
        {
            day = day.AddDays(-1);
        }

        return day;
    }
}
