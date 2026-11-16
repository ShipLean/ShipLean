using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Core;

namespace ShipLean.Application.Features.InspectionMetrics.Aggregate;

// One group of tasks' numbers (design D4): the mean lead time and the mean of each part. Hours are rounded to one decimal, and
// active is what is left of the rounded mean once the rounded blocked and rework means are taken off, so the parts add up to the
// headline exactly. The change is in whole percent against the previous group's unrounded mean.
internal static class LeadTimeStats
{
    public static LeadTimeStatsDto Of(IReadOnlyList<AggregatedTask> tasks, IReadOnlyList<AggregatedTask>? previous = null)
    {
        if (tasks.Count == 0)
        {
            return new LeadTimeStatsDto(0, null, null, null, null, null, null, null, null, null, null, 0, null);
        }

        var mean = Round(tasks.Average(task => task.LeadHours));
        var blocked = Round(tasks.Average(task => task.Lead.BlockedHours));
        var rework = Round(tasks.Average(task => task.Lead.ReworkHours));
        var active = Round(mean - blocked - rework);
        var tail = Round(tasks.Average(task => task.Lead.UnresolvedTailHours));
        return new LeadTimeStatsDto(
            tasks.Count,
            mean,
            DisplayDate.Duration(mean),
            active,
            DisplayDate.Duration(active),
            blocked,
            DisplayDate.Duration(blocked),
            rework,
            DisplayDate.Duration(rework),
            tail,
            DisplayDate.Duration(tail),
            tasks.Count(task => task.Lead.EndedBy == LeadTimeEndedBy.Resolved),
            ChangePct(tasks, previous));
    }

    public static double Round(double hours) => Math.Round(hours, 1, MidpointRounding.AwayFromZero);

    private static int? ChangePct(IReadOnlyList<AggregatedTask> tasks, IReadOnlyList<AggregatedTask>? previous)
    {
        if (previous is null || previous.Count == 0)
        {
            return null;
        }
        var before = previous.Average(task => task.LeadHours);
        if (before <= 0)
        {
            return null;
        }
        return (int)Math.Round((tasks.Average(task => task.LeadHours) - before) / before * 100, MidpointRounding.AwayFromZero);
    }
}
