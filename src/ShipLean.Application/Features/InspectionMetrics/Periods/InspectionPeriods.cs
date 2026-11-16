using System.Globalization;
using ShipLean.Core;

namespace ShipLean.Application.Features.InspectionMetrics.Periods;

// The period rules lead time and quality share (K7, QK5). Only started sprints are periods. An instant falls in the sprint holding
// its day in the viewer's zone, else the one whose weekend (up to two days after its last day) holds it. A sprint belongs to the
// calendar quarter its end date falls in (design D7), and a quarter sums every started sprint ending in it, shown or not.
public static class InspectionPeriods
{
    public const int WeekendDays = 2;

    // The project's sprints that have started by today, oldest first.
    public static IReadOnlyList<InspectionSprint> Started(IEnumerable<InspectionSprint> sprints, DateOnly today)
        => [.. sprints.Where(sprint => sprint.StartDate <= today).OrderBy(sprint => sprint.StartDate)];

    // The sprint an instant falls in, by its day in the zone; null when none does.
    public static InspectionSprint? SprintOf(IReadOnlyList<InspectionSprint> started, DateTimeOffset at, TimeZoneInfo zone)
        => SprintOf(started, DisplayDate.DayOf(at, zone));

    // The sprint a day falls in: the one holding the day, else the one whose weekend holds it.
    public static InspectionSprint? SprintOf(IReadOnlyList<InspectionSprint> started, DateOnly day)
        => started.FirstOrDefault(sprint => sprint.StartDate <= day && day <= sprint.EndDate)
            ?? started.LastOrDefault(sprint => sprint.EndDate < day && day <= sprint.EndDate.AddDays(WeekendDays));

    // The first day of the calendar quarter holding the day.
    public static DateOnly QuarterStart(DateOnly day) => new(day.Year, ((day.Month - 1) / 3 * 3) + 1, 1);

    // The first day of the calendar quarter a sprint belongs to.
    public static DateOnly QuarterOf(InspectionSprint sprint) => QuarterStart(sprint.EndDate);

    // "Q4 2026".
    public static string QuarterLabel(DateOnly quarterStart) => $"Q{((quarterStart.Month - 1) / 3) + 1} {quarterStart.Year.ToString(CultureInfo.InvariantCulture)}";

    // The started sprints a quarter sums: every one ending in it.
    public static IReadOnlyList<InspectionSprint> SprintsEndingIn(IReadOnlyList<InspectionSprint> started, DateOnly quarterStart)
        => [.. started.Where(sprint => QuarterOf(sprint) == quarterStart)];

    // The started sprint before this one, null for the first.
    public static InspectionSprint? Previous(IReadOnlyList<InspectionSprint> started, InspectionSprint sprint)
        => started.LastOrDefault(other => other.StartDate < sprint.StartDate && other.Id != sprint.Id);
}
