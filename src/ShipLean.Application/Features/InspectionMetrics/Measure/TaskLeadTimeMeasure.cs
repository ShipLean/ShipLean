using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.InspectionMetrics.Measure;

// The design's "How the numbers are read" and design D8/D9. Start is the first move to an In-progress state (the timeline's start
// rule, copied rather than shared: item D14). End is the latest commit time, or with none the first move to Resolved or Completed
// at or after the start. Blocked is time with the Blocked tag; rework runs from the first resolve or the first changes-requested
// review to the end, less any blocked time in it; active is the rest. The unresolved tail runs from the last commit to the first
// resolve at or after it and is not counted. Events after the end are listed up to the tail's end but count for nothing.
public sealed class TaskLeadTimeMeasure : ITaskLeadTimeMeasure
{
    public const string NoStartReason = "Closed with no start recorded";

    public TaskLeadTime? Measure(LeadTimeTaskFact task, LeadTimeFacts facts)
    {
        var moves = facts.Moves.Where(m => m.ItemKey == task.Key).OrderBy(m => m.At).ThenBy(m => m.Rev).ToList();
        var startMove = moves.Find(m => m.Category == StateCategory.InProgress);
        var start = startMove?.At;
        var firstResolve = moves.Find(m => IsResolve(m) && (start is null || m.At >= start));
        var commitTimes = facts.Commits.Where(c => c.ItemKey == task.Key).Select(c => c.CommittedAt).OfType<DateTimeOffset>().ToList();
        DateTimeOffset? lastCommit = commitTimes.Count == 0 ? null : commitTimes.Max();
        if ((lastCommit ?? firstResolve?.At) is not { } end)
        {
            return null;
        }

        var endedBy = lastCommit is null ? LeadTimeEndedBy.Resolved : LeadTimeEndedBy.LastCommit;
        var tailEnd = lastCommit is { } last ? moves.Find(m => IsResolve(m) && m.At >= last)?.At : null;
        var shownUntil = tailEnd ?? end;
        var events = Events(task.Key, facts, moves, startMove, start, shownUntil);
        if (start is not { } from)
        {
            return new TaskLeadTime(task.Key, null, end, endedBy, 0, 0, 0, 0, [], events, NoStartReason);
        }

        var blocked = BlockedSpans(task.Key, facts);
        var firstChangesRequested = facts.Reviews
            .Where(r => r.ItemKey == task.Key && r.Verdict == ReviewVerdict.ChangesRequested && r.At >= from && r.At <= end)
            .Select(r => (DateTimeOffset?)r.At)
            .Min();
        var reworkFrom = Earliest(firstResolve?.At, firstChangesRequested) ?? end;
        var segments = Partition(from, end, reworkFrom, blocked);
        if (tailEnd is { } tailTo && tailTo > end)
        {
            segments.Add(new LeadTimeSegment(InspectionSegmentKind.UnresolvedTail, end, tailTo));
        }

        return new TaskLeadTime(
            task.Key,
            from,
            end,
            endedBy,
            Hours(segments, InspectionSegmentKind.Active),
            Hours(segments, InspectionSegmentKind.Blocked),
            Hours(segments, InspectionSegmentKind.Rework),
            Hours(segments, InspectionSegmentKind.UnresolvedTail),
            segments,
            events,
            null);
    }

    private static bool IsResolve(LeadTimeMoveFact move) => move.Category is StateCategory.Resolved or StateCategory.Completed;

    private static DateTimeOffset? Earliest(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null)
        {
            return b;
        }

        return b is null || a <= b ? a : b;
    }

    private static double Hours(IEnumerable<LeadTimeSegment> segments, InspectionSegmentKind kind)
        => segments.Where(s => s.Kind == kind).Sum(s => (s.To - s.From).TotalHours);

    // The spans with the Blocked tag on, from the tag changes in revision order; a tag still on runs to the end of time.
    private static List<(DateTimeOffset From, DateTimeOffset To)> BlockedSpans(string taskKey, LeadTimeFacts facts)
    {
        var spans = new List<(DateTimeOffset From, DateTimeOffset To)>();
        DateTimeOffset? since = null;
        foreach (var change in facts.BlockedTags.Where(t => t.ItemKey == taskKey).OrderBy(t => t.At).ThenBy(t => t.Rev))
        {
            if (change.BlockedAfter)
            {
                since ??= change.At;
            }
            else if (since is { } on)
            {
                spans.Add((on, change.At));
                since = null;
            }
        }
        if (since is { } stillOn)
        {
            spans.Add((stillOn, DateTimeOffset.MaxValue));
        }

        return spans;
    }

    // Start to end cut at every boundary (rework's start, each blocked span's ends) and each piece labelled: blocked wins, then rework
    // from reworkFrom on, else active. Equal neighbours merge, so the segments cover start to end with no gap or overlap.
    private static List<LeadTimeSegment> Partition(
        DateTimeOffset start, DateTimeOffset end, DateTimeOffset reworkFrom, List<(DateTimeOffset From, DateTimeOffset To)> blocked)
    {
        var cuts = blocked.SelectMany(b => new[] { b.From, b.To })
            .Append(reworkFrom)
            .Where(t => t > start && t < end)
            .Append(start)
            .Append(end)
            .Distinct()
            .Order()
            .ToList();
        var segments = new List<LeadTimeSegment>();
        foreach (var (from, to) in cuts.Zip(cuts.Skip(1)))
        {
            var kind = Kind(from, reworkFrom, blocked);
            if (segments.Count > 0 && segments[^1].Kind == kind)
            {
                segments[^1] = segments[^1] with { To = to };
            }
            else
            {
                segments.Add(new LeadTimeSegment(kind, from, to));
            }
        }

        return segments;
    }

    // The kind of the piece starting at from; pieces never straddle a boundary, so their start decides.
    private static InspectionSegmentKind Kind(DateTimeOffset from, DateTimeOffset reworkFrom, List<(DateTimeOffset From, DateTimeOffset To)> blocked)
    {
        if (blocked.Exists(b => b.From <= from && from < b.To))
        {
            return InspectionSegmentKind.Blocked;
        }

        return from >= reworkFrom ? InspectionSegmentKind.Rework : InspectionSegmentKind.Active;
    }

    // What the rail marks, from the start (any time for a task with none) to the end of the tail, in time order.
    private static List<LeadTimeEvent> Events(
        string taskKey, LeadTimeFacts facts, List<LeadTimeMoveFact> moves, LeadTimeMoveFact? startMove, DateTimeOffset? start, DateTimeOffset until)
    {
        var all = new List<LeadTimeEvent>();
        if (startMove is not null)
        {
            all.Add(new LeadTimeEvent(InspectionEventKind.Started, startMove.At, startMove.StateName));
        }
        all.AddRange(moves.Where(IsResolve).Select(m => new LeadTimeEvent(InspectionEventKind.Resolved, m.At, m.StateName)));
        all.AddRange(facts.Commits
            .Where(c => c.ItemKey == taskKey && c.CommittedAt is not null)
            .Select(c => new LeadTimeEvent(InspectionEventKind.Commit, c.CommittedAt!.Value, c.Sha7)));
        all.AddRange(facts.BlockedTags
            .Where(t => t.ItemKey == taskKey)
            .Select(t => t.BlockedAfter
                ? new LeadTimeEvent(InspectionEventKind.Blocked, t.At, "Blocked")
                : new LeadTimeEvent(InspectionEventKind.Unblocked, t.At, "Unblocked")));
        all.AddRange(facts.Reviews
            .Where(r => r.ItemKey == taskKey)
            .Select(r => r.Verdict == ReviewVerdict.ChangesRequested
                ? new LeadTimeEvent(InspectionEventKind.ReviewChangesRequested, r.At, "Changes requested")
                : new LeadTimeEvent(InspectionEventKind.ReviewApproved, r.At, "Approved")));

        return [.. all.Where(e => (start is null || e.At >= start) && e.At <= until).OrderBy(e => e.At)];
    }
}
