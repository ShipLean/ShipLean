using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.Core;

namespace ShipLean.Application.Features.Timeline.Build.Bars;

// The four key dates and the three gaps between them (C1). A point that isn't reached has null fields; a gap needs its From.
internal static class TimelineKeyDates
{
    // The Feature's last move into a completed state while it is still completed; a reopened Feature isn't finished.
    public static TimelineMoveFact? FinishMove(TimelineFacts facts) =>
        facts.Feature.StateCategory != StateCategory.Completed
            ? null
            : facts.Moves.Where(m => m.ItemKey == facts.Feature.Key && m.Category == StateCategory.Completed).OrderBy(m => m.At).ThenBy(m => m.Rev).LastOrDefault();

    public static IReadOnlyList<TimelineKeyDateDto> Dates(
        TimelineLens lens, TimelineFacts facts, TimelineMoveFact? finish, (DateTimeOffset At, string Key, TimelinePersonDto? By, string Move)? execution)
    {
        var feature = facts.Feature;
        var added = Point(lens, KeyDatePoint.Added, feature.CreatedAt, lens.User(feature.CreatedBy), "Feature created");
        var planned = Planned(lens, facts);
        var started = execution is { } e
            ? Point(lens, KeyDatePoint.ExecutionStarted, e.At, e.By, $"{e.Key} moved to {e.Move}")
            : Unreached(KeyDatePoint.ExecutionStarted);
        var finished = finish is null
            ? Unreached(KeyDatePoint.Finished)
            : Point(lens, KeyDatePoint.Finished, finish.At, lens.Mover(finish), $"Feature moved to {finish.StateName}");
        return [added, planned, started, finished];
    }

    public static IReadOnlyList<TimelineGapDto> Gaps(TimelineLens lens, IReadOnlyList<TimelineKeyDateDto> dates)
    {
        var gaps = new List<TimelineGapDto>();
        for (var i = 0; i < dates.Count - 1; i++)
        {
            var (lo, hi) = (dates[i], dates[i + 1]);
            if (lo.At is not { } start)
            {
                gaps.Add(new TimelineGapDto(lo.Point, hi.Point, null, null, false));
                continue;
            }

            var days = ((hi.At ?? lens.AsOf) - start).TotalDays;
            gaps.Add(new TimelineGapDto(lo.Point, hi.Point, TimelineLens.Round(days), DisplayDate.Gap(days), hi.At is null));
        }

        return gaps;
    }

    private static TimelineKeyDateDto Planned(TimelineLens lens, TimelineFacts facts)
    {
        if (facts.PlanAppliedAt is { } applied)
        {
            return Point(lens, KeyDatePoint.Planned, applied, null, "Plan captured");
        }

        return facts.Stories.Count > 0
            ? Point(lens, KeyDatePoint.Planned, facts.Stories.Min(s => s.CreatedAt), null, "First story added")
            : Unreached(KeyDatePoint.Planned);
    }

    private static TimelineKeyDateDto Point(TimelineLens lens, KeyDatePoint point, DateTimeOffset at, TimelinePersonDto? by, string source) =>
        new(point, at, lens.DayLabel(at), by, source);

    private static TimelineKeyDateDto Unreached(KeyDatePoint point) => new(point, null, null, null, null);
}
