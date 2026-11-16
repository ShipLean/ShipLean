using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// The measure (K6) over the design's sample and over hand-built histories: the parts add up, the end rule, SHIP-192's review wait,
// a left-out task, and the segments and events the rail draws.
[TestFixture]
public class TaskLeadTimeMeasureTests
{
    private const string Key = "SHIP-1";
    private const double Tolerance = 1e-9;
    private static readonly DateTimeOffset T0 = LeadTimeFactsBuilder.Start;

    private static TaskLeadTime? Measure(LeadTimeFacts facts, string key) => new TaskLeadTimeMeasure().Measure(facts.Tasks.Single(t => t.Key == key), facts);

    private static TaskLeadTime? Measure(LeadTimeFactsBuilder builder) => Measure(builder.Build(), Key);

    private static LeadTimeFactsBuilder Task() => new LeadTimeFactsBuilder().WithTask(Key);

    private static double LeadHours(TaskLeadTime measured) => (measured.End - measured.Start!.Value).TotalHours;

    // Mutation: leave blocked time inside rework.
    [Test]
    public void Measure_ForAnyTask_ShouldHavePartsThatAddUpToLeadTime()
    {
        var facts = LeadTimeFactsBuilder.Sample();
        var measured = facts.Tasks.Where(t => t.Key != LeadTimeFactsBuilder.SampleLeftOutKey).Select(t => Measure(facts, t.Key)).ToList();

        measured.Count.ShouldBe(LeadTimeFactsBuilder.SampleTaskCount);
        measured.ShouldAllBe(m => m != null && m.Start != null);
        measured.ShouldAllBe(m => Math.Abs(m!.ActiveHours + m.BlockedHours + m.ReworkHours - LeadHours(m)) < 1e-6);
        measured.ShouldContain(m => m!.BlockedHours > 0 && m.ReworkHours > 0);
        measured.ShouldAllBe(m => m!.ActiveHours >= 0 && m.BlockedHours >= 0 && m.ReworkHours >= 0);
    }

    // Mutation: leave blocked time inside rework (rework 6 + blocked 2 would add up to 18, not 16).
    [Test]
    public void Measure_WhenBlockedInsideRework_ShouldCountItAsBlockedOnly()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithResolved(Key, T0.AddHours(8))
            .WithBlocked(Key, T0.AddHours(10), T0.AddHours(12))
            .WithCommit(Key, "aaaaaaa", T0.AddHours(16)));

        measured.ShouldNotBeNull();
        (measured.ActiveHours, measured.BlockedHours, measured.ReworkHours).ShouldBe((8d, 2d, 6d));
    }

    // Mutation: use the last resolve.
    [Test]
    public void Measure_WhenNoCommitTime_ShouldEndAtFirstResolveAndSayResolved()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithResolved(Key, T0.AddHours(5))
            .WithStart(Key, T0.AddHours(6))
            .WithResolved(Key, T0.AddHours(11)));

        measured.ShouldNotBeNull();
        (measured.End, measured.EndedBy, measured.ActiveHours, measured.UnresolvedTailHours).ShouldBe((T0.AddHours(5), LeadTimeEndedBy.Resolved, 5d, 0d));
    }

    // SHIP-192 in the sample: resolved at 14:00 on 6 Oct, changes requested a minute before, fix commit at 11:05 on 7 Oct.
    // Mutation: start rework at the fix commit's previous commit (or leave the review wait in active).
    [Test]
    public void Measure_WhenChangesRequestedThenFixCommit_ShouldCountReviewWaitAsRework()
    {
        var measured = Measure(LeadTimeFactsBuilder.Sample(), "SHIP-192");

        measured.ShouldNotBeNull();
        measured.Start.ShouldBe(new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero));
        (measured.End, measured.EndedBy).ShouldBe((new DateTimeOffset(2026, 10, 7, 11, 5, 0, TimeSpan.Zero), LeadTimeEndedBy.LastCommit));
        measured.ReworkHours.ShouldBe(21.1, Tolerance);
        measured.ActiveHours.ShouldBe(3.4833333333, 1e-6);
        measured.BlockedHours.ShouldBe(0d);
    }

    // Mutation: fall back to the created time.
    [Test]
    public void Measure_WhenNoStartRecorded_ShouldBeLeftOutNeverGivenCreatedAt()
    {
        var measured = Measure(Task()
            .WithMove(Key, T0, "New", StateCategory.Proposed, rev: 1)
            .WithResolved(Key, T0.AddHours(30)));

        measured.ShouldNotBeNull();
        measured.Start.ShouldBeNull();
        measured.LeftOutReason.ShouldBe(TaskLeadTimeMeasure.NoStartReason);
        (measured.ActiveHours, measured.BlockedHours, measured.ReworkHours).ShouldBe((0d, 0d, 0d));
        measured.Segments.ShouldBeEmpty();
    }

    // Mutation: return a lead time for a task that has a start but neither a commit time nor a resolve.
    [Test]
    public void Measure_WhenNotEnded_ShouldBeNull()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithCommit(Key, "aaaaaaa", null));

        measured.ShouldBeNull();
    }

    // Mutation: drop a gap between segments, or let the tail count as part of the lead time.
    [Test]
    public void Segments_ForAnyTask_ShouldCoverStartToEndWithNoGapThenTheTail()
    {
        var facts = LeadTimeFactsBuilder.Sample();
        var measured = facts.Tasks.Where(t => t.Key != LeadTimeFactsBuilder.SampleLeftOutKey).Select(t => Measure(facts, t.Key)!).ToList();

        measured.ShouldAllBe(m => Covers(m));
    }

    // Mutation: label the blocked span inside rework as rework.
    [Test]
    public void Segments_WhenBlockedAndReworked_ShouldRunInOrderAndEndWithTheTail()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithBlocked(Key, T0.AddHours(2), T0.AddHours(4))
            .WithReview(Key, T0.AddHours(10), ReviewVerdict.ChangesRequested)
            .WithBlocked(Key, T0.AddHours(12), T0.AddHours(13))
            .WithCommit(Key, "aaaaaaa", T0.AddHours(16))
            .WithResolved(Key, T0.AddHours(18)));

        measured.ShouldNotBeNull();
        measured.Segments.Select(s => (s.Kind, (s.From - T0).TotalHours, (s.To - T0).TotalHours)).ShouldBe(new[]
        {
            (InspectionSegmentKind.Active, 0d, 2d),
            (InspectionSegmentKind.Blocked, 2d, 4d),
            (InspectionSegmentKind.Active, 4d, 10d),
            (InspectionSegmentKind.Rework, 10d, 12d),
            (InspectionSegmentKind.Blocked, 12d, 13d),
            (InspectionSegmentKind.Rework, 13d, 16d),
            (InspectionSegmentKind.UnresolvedTail, 16d, 18d),
        });
        (measured.BlockedHours, measured.ReworkHours, measured.UnresolvedTailHours).ShouldBe((3d, 5d, 2d));
    }

    // Mutation: let a changes-requested review or a blocked span after the end add rework or blocked hours.
    [Test]
    public void Events_WhenAfterEnd_ShouldNotCount()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithCommit(Key, "aaaaaaa", T0.AddHours(6))
            .WithBlocked(Key, T0.AddHours(7), T0.AddHours(8))
            .WithReview(Key, T0.AddHours(8.5), ReviewVerdict.ChangesRequested)
            .WithResolved(Key, T0.AddHours(9)));

        measured.ShouldNotBeNull();
        (measured.End, measured.ActiveHours, measured.BlockedHours, measured.ReworkHours, measured.UnresolvedTailHours)
            .ShouldBe((T0.AddHours(6), 6d, 0d, 0d, 3d));
    }

    // Mutation: list events past the tail's end, or leave out a kind the rail draws.
    [Test]
    public void Events_ForATask_ShouldListEachPointInOrderUpToTheTailEnd()
    {
        var measured = Measure(Task()
            .WithMove(Key, T0.AddHours(-5), "New", StateCategory.Proposed, rev: 1)
            .WithStart(Key, T0)
            .WithBlocked(Key, T0.AddHours(1), T0.AddHours(2))
            .WithCommit(Key, "b71e0d4", T0.AddHours(3))
            .WithResolved(Key, T0.AddHours(4))
            .WithReview(Key, T0.AddHours(5), ReviewVerdict.ChangesRequested)
            .WithCommit(Key, "9f3c2a1", T0.AddHours(7))
            .WithReview(Key, T0.AddHours(7.5), ReviewVerdict.Approved)
            .WithResolved(Key, T0.AddHours(8))
            .WithReview(Key, T0.AddHours(9), ReviewVerdict.Approved));

        measured.ShouldNotBeNull();
        measured.Events.Select(e => (e.Kind, (e.At - T0).TotalHours, e.Text)).ShouldBe(new[]
        {
            (InspectionEventKind.Started, 0d, LeadTimeFactsBuilder.InProgressState),
            (InspectionEventKind.Blocked, 1d, "Blocked"),
            (InspectionEventKind.Unblocked, 2d, "Unblocked"),
            (InspectionEventKind.Commit, 3d, "b71e0d4"),
            (InspectionEventKind.Resolved, 4d, LeadTimeFactsBuilder.ResolvedState),
            (InspectionEventKind.ReviewChangesRequested, 5d, "Changes requested"),
            (InspectionEventKind.Commit, 7d, "9f3c2a1"),
            (InspectionEventKind.ReviewApproved, 7.5d, "Approved"),
            (InspectionEventKind.Resolved, 8d, LeadTimeFactsBuilder.ResolvedState),
        });
        (measured.ReworkHours, measured.UnresolvedTailHours).ShouldBe((3d, 1d));
    }

    private static bool Covers(TaskLeadTime m)
    {
        var counted = m.Segments.Where(s => s.Kind != InspectionSegmentKind.UnresolvedTail).ToList();
        var tail = m.Segments.Where(s => s.Kind == InspectionSegmentKind.UnresolvedTail).ToList();
        var contiguous = counted.Zip(counted.Skip(1)).All(p => p.First.To == p.Second.From && p.First.Kind != p.Second.Kind);
        var tailOk = tail.Count == 0 ? m.UnresolvedTailHours <= 0 : tail.Count == 1 && tail[0].From == m.End && m.Segments[^1] == tail[0];
        return counted.Count > 0 && counted[0].From == m.Start && counted[^1].To == m.End && contiguous && counted.TrueForAll(s => s.To > s.From) && tailOk;
    }
}
