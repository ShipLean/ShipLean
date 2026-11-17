using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Periods;

// The days whose facts a view reads (K3), and the sprints it shows. Shown: the last 6 started sprints (the running one included),
// the sprints ending in today's calendar quarter or the one before it, or every started sprint; a sprint belongs to the quarter
// its end date falls in (design D7). From is the first day of the calendar quarter before the one holding the first shown sprint,
// which covers the change against the previous sprint, against last quarter, and quarter rows that sum every sprint of their
// quarter. All instead runs from the project's first sprint. An opened period outside the range widens From the same way. With
// nothing shown, From is the start of the quarter before today's. To is today.
public sealed record InspectionWindow(DateOnly From, DateOnly To, IReadOnlyList<InspectionSprint> Shown)
{
    public static InspectionWindow For(InspectionRange range, IReadOnlyList<InspectionSprint> sprints, DateOnly today, InspectionSprint? period = null)
    {
        var started = InspectionPeriods.Started(sprints, today);
        var thisQuarter = QuarterStart(today);
        var lastQuarter = thisQuarter.AddMonths(-3);
        IReadOnlyList<InspectionSprint> shown = range switch
        {
            InspectionRange.Last6 => [.. started.TakeLast(6)],
            InspectionRange.Quarter => [.. started.Where(sprint => QuarterStart(sprint.EndDate) == thisQuarter)],
            InspectionRange.LastQuarter => [.. started.Where(sprint => QuarterStart(sprint.EndDate) == lastQuarter)],
            InspectionRange.All => started,
            _ => throw new ArgumentOutOfRangeException(nameof(range), range, null),
        };

        var from = lastQuarter;
        if (shown.Count > 0)
        {
            from = range == InspectionRange.All ? shown[0].StartDate : QuarterStart(shown[0].EndDate).AddMonths(-3);
        }
        if (period is not null)
        {
            var periodFrom = QuarterStart(period.EndDate).AddMonths(-3);
            from = periodFrom < from ? periodFrom : from;
        }
        return new InspectionWindow(from, today, shown);
    }

    // The first day of the calendar quarter holding the day.
    public static DateOnly QuarterStart(DateOnly day) => InspectionPeriods.QuarterStart(day);
}
