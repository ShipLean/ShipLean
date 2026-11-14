using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.Core;

namespace ShipLean.Application.Features.Timeline.Build.Bars;

// The chart's time axis (C1): calendar days in the viewer's zone from the first start to the last end, weekends marked, sprint bands
// clipped to the days drawn. Sets the lens's origin, so call it before any hour offset is made.
internal static class TimelineScaleBuilder
{
    public static TimelineScaleDto Build(
        TimelineLens lens, TimelineFacts facts, DateTimeOffset? firstAt, DateTimeOffset? lastAt, DateTimeOffset? finishedAt)
    {
        var zone = lens.Zone;
        var first = DisplayDate.DayOf(firstAt ?? lens.AsOf, zone);
        var last = finishedAt is null ? DisplayDate.DayOf(lens.AsOf, zone) : DisplayDate.DayOf(finishedAt.Value, zone);
        if (lastAt is { } latest && DisplayDate.DayOf(latest, zone) > last)
        {
            last = DisplayDate.DayOf(latest, zone);
        }
        if (last < first)
        {
            last = first;
        }

        lens.Origin = DisplayDate.StartOfDay(first, zone);
        var days = new List<TimelineDayDto>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var start = DisplayDate.StartOfDay(day, zone);
            days.Add(new TimelineDayDto(
                lens.HoursAt(start), DisplayDate.WeekdayDay(day), day.Day.ToString(System.Globalization.CultureInfo.InvariantCulture),
                day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday, day.Day == 1));
        }

        var endHours = lens.HoursAt(DisplayDate.StartOfDay(last.AddDays(1), zone));
        var sprints = new List<TimelineSprintDto>();
        foreach (var sprint in facts.Sprints)
        {
            var lo = Math.Max(0, lens.HoursAt(DisplayDate.StartOfDay(sprint.StartDate, zone)));
            var hi = Math.Min(endHours, lens.HoursAt(DisplayDate.StartOfDay(sprint.EndDate.AddDays(1), zone)));
            if (hi > lo)
            {
                sprints.Add(new TimelineSprintDto(sprint.Name, lo, hi));
            }
        }

        var nowHours = finishedAt is null ? lens.HoursAt(lens.AsOf) : (double?)null;
        return new TimelineScaleDto(lens.Origin, endHours, nowHours, days, sprints);
    }
}
