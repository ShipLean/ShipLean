using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// What the real measure (B2) must do, by the design's "How the numbers are read" (K6). Red until B2 replaces the stub: each test
// fails because the stub measures nothing. The measure is built directly, as the service registers it.
[TestFixture]
public class TaskLeadTimeMeasureContractTests
{
    private const string Key = "SHIP-1";
    private static readonly DateTimeOffset T0 = LeadTimeFactsBuilder.Start;

    private static TaskLeadTime? Measure(LeadTimeFactsBuilder builder)
    {
        var facts = builder.Build();
        return new TaskLeadTimeMeasure().Measure(facts.Tasks.Single(t => t.Key == Key), facts);
    }

    private static LeadTimeFactsBuilder Task() => new LeadTimeFactsBuilder().WithTask(Key);

    // Mutation: start at the last move to In progress instead of the first.
    [Test]
    public void Start_WhenMovedToInProgressTwice_ShouldBeTheFirstMoveTurnedGreenByB2()
    {
        var measured = Measure(Task()
            .WithMove(Key, T0.AddHours(1), "New", StateCategory.Proposed)
            .WithStart(Key, T0.AddHours(2))
            .WithStart(Key, T0.AddHours(10))
            .WithCommit(Key, "abc1234", T0.AddHours(12)));

        (measured?.Start).ShouldBe(T0.AddHours(2));
    }

    // Mutation: end at the first commit instead of the latest.
    [Test]
    public void End_WhenCommitsHaveTimes_ShouldBeTheLastCommitAndTheTailRunsToResolvedTurnedGreenByB2()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithCommit(Key, "aaaaaaa", T0.AddHours(8))
            .WithCommit(Key, "bbbbbbb", T0.AddHours(5))
            .WithResolved(Key, T0.AddHours(12)));

        (measured?.End, measured?.EndedBy, measured?.UnresolvedTailHours).ShouldBe((T0.AddHours(8), LeadTimeEndedBy.LastCommit, 4d));
    }

    // Mutation: end at the last commit even when no commit has a time (reading the null as the start).
    [Test]
    public void End_WhenNoCommitHasATime_ShouldBeTheFirstResolvedTurnedGreenByB2()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithCommit(Key, "aaaaaaa", null)
            .WithResolved(Key, T0.AddHours(6))
            .WithResolved(Key, T0.AddHours(9)));

        (measured?.End, measured?.EndedBy, measured?.UnresolvedTailHours).ShouldBe((T0.AddHours(6), LeadTimeEndedBy.Resolved, 0d));
    }

    // Mutation: count blocked time twice (once in blocked and once in active).
    [Test]
    public void Parts_WhenBlockedAndReworked_ShouldAddUpToTheLeadTimeTurnedGreenByB2()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithBlocked(Key, T0.AddHours(2), T0.AddHours(4))
            .WithReview(Key, T0.AddHours(10), ReviewVerdict.ChangesRequested)
            .WithCommit(Key, "aaaaaaa", T0.AddHours(16))
            .WithResolved(Key, T0.AddHours(18)));

        (measured?.ActiveHours, measured?.BlockedHours, measured?.ReworkHours).ShouldBe((8d, 2d, 6d));
        ((measured?.ActiveHours ?? 0) + (measured?.BlockedHours ?? 0) + (measured?.ReworkHours ?? 0)).ShouldBe(16d);
    }

    // Mutation: start rework at the last resolve instead of the first.
    [Test]
    public void Rework_WhenResolvedBeforeTheLastCommit_ShouldRunFromTheFirstResolveTurnedGreenByB2()
    {
        var measured = Measure(Task()
            .WithStart(Key, T0)
            .WithResolved(Key, T0.AddHours(6))
            .WithStart(Key, T0.AddHours(7))
            .WithResolved(Key, T0.AddHours(9))
            .WithCommit(Key, "aaaaaaa", T0.AddHours(14)));

        (measured?.ActiveHours, measured?.BlockedHours, measured?.ReworkHours).ShouldBe((6d, 0d, 8d));
    }

    // Mutation: give a task with no start the time of its first resolve as its start.
    [Test]
    public void Measure_WhenEndedWithNoStart_ShouldBeLeftOutWithAReasonAndNoStartTurnedGreenByB2()
    {
        var measured = Measure(Task()
            .WithResolved(Key, T0.AddHours(5)));

        measured.ShouldNotBeNull();
        (measured.Start, measured.EndedBy, measured.End).ShouldBe((null, LeadTimeEndedBy.Resolved, T0.AddHours(5)));
        measured.LeftOutReason.ShouldNotBeNullOrWhiteSpace();
    }
}
