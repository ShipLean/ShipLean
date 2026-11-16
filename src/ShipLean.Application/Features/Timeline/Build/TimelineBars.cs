using ShipLean.Application.Features.Timeline.Build.Bars;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.Core;

namespace ShipLean.Application.Features.Timeline.Build;

// Turns the facts into the header, key dates, gaps, execution, scale, story brackets and task bars, in the viewer's zone. Pure.
public static class TimelineBars
{
    public static TimelineBarsPart Build(TimelineFacts facts, DateTimeOffset asOf, TimeZoneInfo zone)
    {
        var lens = new TimelineLens(asOf, zone, facts.People);
        var feature = facts.Feature;
        var header = new TimelineFeatureDto(
            feature.Id, feature.Key, feature.Title, feature.StateName, feature.StateCategory,
            feature.Epic is null ? null : new TimelineEpicDto(feature.Epic.Key, feature.Epic.Title));

        // Removed stories and bugs are left out with their tasks; so are removed tasks.
        var parents = facts.Stories.Where(s => s.StateCategory != StateCategory.Removed).ToList();
        var kept = parents.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var tasks = facts.Tasks.Where(t => t.StateCategory != StateCategory.Removed && kept.Contains(t.ParentKey)).ToList();
        var traces = tasks.Select(t => (Task: t, Trace: TaskTrace.Of(t, facts))).ToList();
        var traced = traces.Where(t => t.Trace is not null).Select(t => (t.Task, Trace: t.Trace!)).ToList();

        var finish = TimelineKeyDates.FinishMove(facts);
        var scale = TimelineScaleBuilder.Build(
            lens, facts, traced.Count == 0 ? null : traced.Min(t => t.Trace.Anchor),
            traced.Select(t => t.Trace.End).Max(), finish?.At);

        var bars = new Dictionary<string, TimelineBarDto>(StringComparer.Ordinal);
        foreach (var (task, trace) in traced)
        {
            bars[task.Key] = Bar(lens, facts, task, trace);
        }

        var stories = new List<TimelineStoryDto>();
        var notStarted = new List<TimelineNotStartedDto>();
        foreach (var parent in parents)
        {
            var children = traced.Where(t => t.Task.ParentKey == parent.Key).OrderBy(t => t.Trace.Anchor).ThenBy(t => t.Task.Key, StringComparer.Ordinal).ToList();
            var idle = traces.Where(t => t.Task.ParentKey == parent.Key && t.Trace is null).Select(t => t.Task).ToList();
            if (children.Count + idle.Count == 0 && parent.StateCategory == StateCategory.Proposed)
            {
                notStarted.Add(new TimelineNotStartedDto(parent.Key, parent.Type, parent.Title, null));
                continue;
            }

            stories.Add(Bracket(lens, parent, children.Select(c => c.Trace).ToList(), idle.Count, children.Select(c => c.Task.Key).ToList()));
        }

        notStarted.AddRange(traces.Where(t => t.Trace is null).Select(t => new TimelineNotStartedDto(t.Task.Key, WorkItemTypeId.Task, t.Task.Title, t.Task.ParentKey)));

        var ordered = stories.OrderBy(s => s.Start is null ? 1 : 0).ThenBy(s => s.Start).ToList();
        var barList = ordered.SelectMany(s => s.Tasks).Select(key => bars[key]).ToList();

        var firstStart = traced.Where(t => !t.Trace.Inferred).OrderBy(t => t.Trace.Start).ThenBy(t => t.Task.Key, StringComparer.Ordinal).FirstOrDefault();
        (DateTimeOffset, string, TimelinePersonDto?, string)? execution = firstStart.Trace is null
            ? null
            : (firstStart.Trace.Start!.Value, firstStart.Task.Key, bars[firstStart.Task.Key].Assignee, firstStart.Trace.StartMove!.StateName);
        var dates = TimelineKeyDates.Dates(lens, facts, finish, execution);
        var execStart = dates[2].At;
        var execEnd = dates[3].At;
        var calendarDays = execStart is { } s0 ? TimelineLens.Round(((execEnd ?? asOf) - s0).TotalDays) : (double?)null;

        return new TimelineBarsPart(
            header, finish is not null, dates, TimelineKeyDates.Gaps(lens, dates), new TimelineExecutionDto(execStart, execEnd, calendarDays),
            scale, ordered, barList, notStarted);
    }

    private static TimelineBarDto Bar(TimelineLens lens, TimelineFacts facts, TimelineTaskFact task, TaskTrace trace)
    {
        var assignee = trace.StartMove?.AgentName is { } agent ? TimelineLens.Agent(agent) : lens.User(task.AssigneeId);
        double? hours = trace.Start is { } start ? TimelineLens.Round(((trace.End ?? lens.AsOf) - start).TotalHours) : null;
        var raw = trace.Start is { } s ? ((trace.End ?? lens.AsOf) - s).TotalHours : 0;
        var history = new List<TimelinePeriodDto>();
        for (var i = 0; i < trace.Periods.Count; i++)
        {
            var period = trace.Periods[i];
            var finalOne = i == trace.Periods.Count - 1;
            var endsHere = period.State is TimelinePeriodState.Resolved or TimelinePeriodState.Closed;
            DateTimeOffset? to = null;
            if (!finalOne)
            {
                to = trace.Periods[i + 1].From;
            }
            else if (!endsHere)
            {
                to = lens.AsOf;
            }

            var length = to is { } t ? (t - period.From).TotalHours : (double?)null;
            history.Add(new TimelinePeriodDto(
                period.State, period.StateName, period.From, to, lens.DayTime(period.From),
                length is { } l ? TimelineLens.Round(l) : null, length is { } l2 ? DisplayDate.Duration(l2) : null));
        }

        var reviews = facts.Reviews.Where(r => r.ItemKey == task.Key).OrderBy(r => r.At)
            .Select(r => new TimelineReviewDto(
                r.At, lens.HoursAt(r.At), lens.DayTime(r.At), r.Verdict,
                r.AgentName is { } a ? TimelineLens.Agent(a) : lens.User(r.ByUserId)!, r.Note))
            .ToList();
        return new TimelineBarDto(
            task.Key, WorkItemTypeId.Task, task.Title, task.ParentKey, assignee, trace.State, trace.Start, trace.End,
            trace.Start is { } st ? lens.HoursAt(st) : null, trace.End is { } en ? lens.HoursAt(en) : null, hours,
            trace.Start is { } st2 ? lens.DayTime(st2) : null, trace.End is { } en2 ? lens.DayTime(en2) : null,
            trace.Start is null ? null : DisplayDate.Duration(raw), trace.Inferred, false, history, reviews);
    }

    private static TimelineStoryDto Bracket(TimelineLens lens, TimelineStoryFact story, List<TaskTrace> children, int idle, List<string> taskKeys)
    {
        var starts = children.Where(c => !c.Inferred).Select(c => c.Start!.Value).ToList();
        DateTimeOffset? start = starts.Count == 0 ? null : starts.Min();
        var allEnded = idle == 0 && children.Count > 0 && children.All(c => c.End is not null);
        DateTimeOffset? end = start is not null && allEnded ? children.Max(c => c.End) : null;
        double? hours = start is { } s ? TimelineLens.Round(((end ?? lens.AsOf) - s).TotalHours) : null;
        return new TimelineStoryDto(
            story.Key, story.Type, story.Title, lens.User(story.AssigneeId), start, end,
            start is { } x1 ? lens.HoursAt(x1) : null, end is { } x2 ? lens.HoursAt(x2) : null, hours,
            start is { } x3 ? lens.DayTime(x3) : null, end is { } x4 ? lens.DayTime(x4) : null,
            start is { } x5 ? DisplayDate.Duration(((end ?? lens.AsOf) - x5).TotalHours) : null, taskKeys);
    }
}
