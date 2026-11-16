using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline.Build.Bars;

// What one task's state history says (D2, D4, D16): when its bar starts and ends, the move that started it, and its periods. A
// trace is only made for a task that moved past New or was closed; Start is null for an inferred one, whose diamond sits at End.
internal sealed record TaskTrace(
    TimelineBarState State,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    TimelineMoveFact? StartMove,
    IReadOnlyList<RawPeriod> Periods)
{
    public bool Inferred => Start is null;

    // The instant the bar is first drawn at: its start, or its diamond.
    public DateTimeOffset Anchor => Start ?? End!.Value;

    // Null when the task never left New (and was never closed), so it belongs under "not started".
    public static TaskTrace? Of(TimelineTaskFact task, TimelineFacts facts)
    {
        var moves = facts.Moves.Where(m => m.ItemKey == task.Key).OrderBy(m => m.At).ThenBy(m => m.Rev).ToList();
        var lastActive = moves.FindLastIndex(m => m.Category == StateCategory.InProgress);
        var firstDone = moves.FindIndex(m => m.Category is StateCategory.Resolved or StateCategory.Completed);
        if (lastActive < 0 && firstDone < 0)
        {
            return null;
        }

        var closed = task.StateCategory == StateCategory.Completed;
        if (lastActive < 0)
        {
            var diamond = moves[firstDone].At;
            return new TaskTrace(closed ? TimelineBarState.Closed : TimelineBarState.Resolved, null, diamond, null, BuildPeriods(task, facts, moves, diamond));
        }

        var startMove = moves.First(m => m.Category == StateCategory.InProgress);
        var after = moves.Skip(lastActive + 1).ToList();
        var end = after.LastOrDefault(m => m.Category == StateCategory.Resolved)?.At
            ?? after.FirstOrDefault(m => m.Category == StateCategory.Completed)?.At;
        var state = closed ? TimelineBarState.Closed : TimelineBarState.Resolved;
        if (end is null)
        {
            state = TimelineBarState.InFlight;
        }

        return new TaskTrace(state, startMove.At, end, startMove, BuildPeriods(task, facts, moves, startMove.At));
    }

    // Periods from the move that starts the bar on: each move's category and the Blocked tag give the state; equal neighbours merge.
    private static List<RawPeriod> BuildPeriods(TimelineTaskFact task, TimelineFacts facts, List<TimelineMoveFact> moves, DateTimeOffset from)
    {
        var events = moves.Select(m => (m.At, m.Rev, Move: (TimelineMoveFact?)m, Tag: (TimelineBlockedTagFact?)null))
            .Concat(facts.BlockedTags.Where(t => t.ItemKey == task.Key).Select(t => (t.At, t.Rev, Move: (TimelineMoveFact?)null, Tag: (TimelineBlockedTagFact?)t)))
            .OrderBy(e => e.At).ThenBy(e => e.Rev);
        var periods = new List<RawPeriod>();
        var category = StateCategory.Proposed;
        var name = "New";
        var blocked = false;
        foreach (var (at, _, move, tag) in events)
        {
            if (move is not null)
            {
                category = move.Category;
                name = move.StateName;
            }
            if (tag is not null)
            {
                blocked = tag.BlockedAfter;
            }

            var state = category switch
            {
                StateCategory.Proposed => TimelinePeriodState.New,
                StateCategory.InProgress => blocked ? TimelinePeriodState.Blocked : TimelinePeriodState.Active,
                StateCategory.Resolved => TimelinePeriodState.Resolved,
                StateCategory.Completed => TimelinePeriodState.Closed,
                _ => TimelinePeriodState.Removed,
            };
            if (at >= from && (periods.Count == 0 || periods[^1].State != state || periods[^1].StateName != name))
            {
                periods.Add(new RawPeriod(state, name, at));
            }
        }

        return periods;
    }
}

internal sealed record RawPeriod(TimelinePeriodState State, string StateName, DateTimeOffset From);
