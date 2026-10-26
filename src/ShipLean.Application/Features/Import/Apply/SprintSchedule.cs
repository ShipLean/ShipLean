using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Features.Import.Apply;

// The dates every Jira sprint gets in ShipLean, where sprints may not overlap (end dates included). A dated sprint that overlaps one before it
// (Jira hands over on the same day, or runs sprints in parallel) starts the day after that sprint ends and keeps its length. An undated (future)
// sprint comes after the last one, back to back, as long as that sprint (two weeks when no sprint has dates). Each change is named in a warning.
internal static class SprintSchedule
{
    private const int DefaultLengthDays = 14;

    public static (Dictionary<long, (DateOnly Start, DateOnly End)> Dates, List<string> Warnings) Plan(IReadOnlyList<JiraSprint> all, DateOnly today)
    {
        var dates = new Dictionary<long, (DateOnly Start, DateOnly End)>();
        var warnings = new List<string>();
        (DateOnly End, string Name)? last = null;
        var lastLength = DefaultLengthDays;
        var dated = all.Where(s => s.StartDate is not null && s.EndDate is not null)
            .Select(s => (Sprint: s, Start: DateOnly.FromDateTime(s.StartDate!.Value.UtcDateTime), End: DateOnly.FromDateTime(s.EndDate!.Value.UtcDateTime)))
            .OrderBy(s => s.Start).ThenBy(s => s.End).ThenBy(s => s.Sprint.Id);
        foreach (var (sprint, plannedStart, plannedEnd) in dated)
        {
            var (start, end) = (plannedStart, plannedEnd);
            var length = Math.Max(plannedEnd.DayNumber - plannedStart.DayNumber, 0);
            if (last is { } before && start <= before.End)
            {
                var moved = before.End.AddDays(1);
                warnings.Add($"Sprint {sprint.Name} overlaps {before.Name}, so it starts on {moved:yyyy-MM-dd}, the day after {before.Name} ends, instead of {start:yyyy-MM-dd}.");
                (start, end) = (moved, moved.AddDays(length));
            }

            dates[sprint.Id] = (start, end);
            if (last is null || end > last.Value.End)
            {
                last = (end, sprint.Name);
                lastLength = Math.Max(length, 1);
            }
        }

        var next = last?.End.AddDays(1) ?? today;
        foreach (var sprint in all.Where(s => !dates.ContainsKey(s.Id)))
        {
            dates[sprint.Id] = (next, next.AddDays(lastLength));
            next = next.AddDays(lastLength + 1);
        }

        return (dates, warnings);
    }
}
