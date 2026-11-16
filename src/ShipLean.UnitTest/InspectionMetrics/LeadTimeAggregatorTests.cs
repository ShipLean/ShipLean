using ShipLean.Application.Features.InspectionMetrics;
using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// The aggregator (K7) over hand-made TaskLeadTime values, never the real measure. Sprints as in the contract tests: Sprint 1 (Mon 7
// Sep to Fri 18 Sep) ends in Q3, Sprints 2 to 4 (21 Sep to 2 Oct, 5 to 16 Oct, 19 to 30 Oct) end in Q4. Sprint 5 (2 to 13 Nov) has
// not started. Today is Sat 24 Oct 2026, inside Sprint 4.
[TestFixture]
public class LeadTimeAggregatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ana = new("00000000-0000-4000-8000-000000000a01");
    private static readonly Guid Tom = new("00000000-0000-4000-8000-000000000a02");

    private static readonly InspectionSprint S1 = Sprint(1, new DateOnly(2026, 9, 7));
    private static readonly InspectionSprint S2 = Sprint(2, new DateOnly(2026, 9, 21));
    private static readonly InspectionSprint S3 = Sprint(3, new DateOnly(2026, 10, 5));
    private static readonly InspectionSprint S4 = Sprint(4, new DateOnly(2026, 10, 19));
    private static readonly InspectionSprint S5 = Sprint(5, new DateOnly(2026, 11, 2));
    private static readonly InspectionSprint[] All = [S1, S2, S3, S4, S5];

    private static InspectionSprint Sprint(int number, DateOnly start)
        => new(new Guid($"00000000-0000-4000-8000-00000000000{number}"), $"Sprint {number}", start, start.AddDays(11));

    private static DateTimeOffset At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static TaskLeadTime Task(string key, DateTimeOffset end, double hours, double blocked = 0, double rework = 0, LeadTimeEndedBy endedBy = LeadTimeEndedBy.LastCommit)
        => new(key, end.AddHours(-hours), end, endedBy, hours - blocked - rework, blocked, rework, 0, [], [], null);

    private static InspectionMetricsViewDto Build(
        InspectionSprint[] shown, LeadTimeFactsBuilder facts, IReadOnlyList<TaskLeadTime> tasks, InspectionCut cut = InspectionCut.None, InspectionSprint? period = null, string? select = null)
    {
        var sprints = new InspectionSprints(new InspectionProjectDto(Guid.NewGuid(), "SHIP", "ShipLean"), All);
        var window = new InspectionWindow(new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 24), shown);
        var selection = new InspectionMetricsSelection(InspectionRange.Last6, cut, period, select);
        return new LeadTimeAggregator().Build(sprints, window, facts.Build(), tasks, selection, Now, TimeZoneInfo.Utc);
    }

    private static InspectionMetricsViewDto Build(InspectionSprint[] shown, params TaskLeadTime[] tasks)
        => Build(shown, tasks.Aggregate(new LeadTimeFactsBuilder(), (builder, task) => builder.WithTask(task.TaskKey)), tasks);

    // Mutation: group by sprint_id (the sprint it was planned in).
    [Test]
    public void Build_WhenTaskMovedSprints_ShouldCountItOnceInSprintItEnded()
    {
        // Planned in Sprint 2, started there, moved to Sprint 3 and ended there.
        var view = Build([S2, S3, S4], Task("SHIP-1", At(10, 7, 12), 200), Task("SHIP-2", At(9, 30, 12), 4));

        view.Periods.Select(p => (p.Id, p.LeadTime.Tasks)).ShouldBe([(S2.Id, 1), (S3.Id, 1), (S4.Id, 0)]);
        view.Periods.SelectMany(p => p.Dots).Select(d => d.TaskKey).ShouldBe(["SHIP-2", "SHIP-1"]);
        view.Coverage.Tasks.ShouldBe(2);
    }

    // Mutation: sum only the shown periods.
    [Test]
    public void Build_WhenRangeIsLast6_ShouldStillSumWholeQuarterInQuarterRow()
    {
        var view = Build([S3, S4], Task("SHIP-1", At(9, 22, 12), 30), Task("SHIP-2", At(10, 6, 12), 10), Task("SHIP-3", At(9, 10, 12), 99));

        var quarter = view.Quarters.ShouldHaveSingleItem();
        (quarter.Quarter, quarter.ToDate, quarter.LeadTime.Tasks, quarter.LeadTime.MeanHours).ShouldBe(("Q4 2026", true, 2, 20d));
        quarter.PeriodIds.ShouldBe([S2.Id, S3.Id, S4.Id]);
        view.Headline.LastQuarter.ShouldNotBeNull().LeadTime.MeanHours.ShouldBe(99d);
    }

    // Mutation: pick the last period.
    [Test]
    public void Build_WhenSprintRunning_ShouldHeadlineLastFinishedSprint()
    {
        var view = Build([S2, S3, S4], Task("SHIP-1", At(10, 7, 12), 20), Task("SHIP-2", At(10, 20, 12), 50), Task("SHIP-3", At(9, 22, 12), 10));

        var finished = view.Headline.Finished.ShouldNotBeNull();
        (finished.PeriodId, finished.Name, finished.LeadTime.MeanHours, finished.LeadTime.ChangePctVsPrevious).ShouldBe((S3.Id, "Sprint 3", 20d, 100));
        view.Headline.SoFar.ShouldNotBeNull().PeriodId.ShouldBe(S4.Id);
        view.Periods.Select(p => p.InProgress).ShouldBe([false, false, true]);
        view.Periods.Select(p => p.Id).ShouldNotContain(S5.Id);
    }

    // Mutation: group by assignee id.
    [Test]
    public void Build_WhenCutByWho_ShouldGroupNullAgentNameAsPeopleAndEachAgentNameSeparately()
    {
        var facts = new LeadTimeFactsBuilder()
            .WithTask("SHIP-1", assigneeId: Ana, assigneeName: "Ana", agentName: "Claude Code")
            .WithTask("SHIP-2", assigneeId: Ana, assigneeName: "Ana", agentName: "Cursor")
            .WithTask("SHIP-3", assigneeId: Ana, assigneeName: "Ana", agentName: "Claude Code")
            .WithTask("SHIP-4", assigneeId: Ana, assigneeName: "Ana")
            .WithTask("SHIP-5", assigneeId: Tom, assigneeName: "Tom");
        TaskLeadTime[] tasks =
        [
            Task("SHIP-1", At(10, 6, 12), 10), Task("SHIP-2", At(10, 7, 12), 20), Task("SHIP-3", At(10, 20, 12), 30),
            Task("SHIP-4", At(10, 8, 12), 40), Task("SHIP-5", At(10, 21, 12), 60),
        ];

        var view = Build([S3, S4], facts, tasks, InspectionCut.Who);

        view.Cuts.ShouldNotBeNull().Select(c => (c.Key, c.Label, c.Agent, c.All.Tasks, c.All.MeanHours))
            .ShouldBe([("Claude Code", "Claude Code", true, 2, 20d), ("people", "People", false, 2, 50d), ("Cursor", "Cursor", true, 1, 20d)]);
        view.Cuts!.ShouldAllBe(c => c.Cells.Count == 2);
        view.Cuts![1].Cells.Select(c => c.MeanHours).ShouldBe([40d, 60d]);
    }

    [Test]
    public void Build_WhenPeriodGiven_ShouldOrderTasksSlowestFirst()
    {
        var view = Build([S3, S4], new LeadTimeFactsBuilder().WithTask("SHIP-1").WithTask("SHIP-2").WithTask("SHIP-3"),
            [Task("SHIP-1", At(10, 6, 12), 10), Task("SHIP-2", At(10, 7, 12), 70), Task("SHIP-3", At(10, 8, 12), 30)], period: S3);

        var detail = view.PeriodDetail.ShouldNotBeNull();
        detail.Tasks.Select(t => t.Key).ShouldBe(["SHIP-2", "SHIP-3", "SHIP-1"]);
        (detail.Quarter, detail.Previous?.ShortName, detail.Next?.ShortName, detail.InProgress).ShouldBe(("Q4 2026", "S2", "S4", false));
        detail.Selected.ShouldBeNull();
    }

    // Mutation: round each part on its own (the active mean no longer the remainder).
    [Test]
    public void Build_WhenPartsRoundApart_ShouldStillAddUpToTheMean()
    {
        var view = Build([S3], Task("SHIP-1", At(10, 6, 12), 10.04, blocked: 1.04, rework: 1.04), Task("SHIP-2", At(10, 7, 12), 10.11, blocked: 1.11, rework: 1.11));

        var stats = view.Periods.Single().LeadTime;
        (stats.MeanActiveHours + stats.MeanBlockedHours + stats.MeanReworkHours).ShouldNotBeNull().ShouldBe(stats.MeanHours!.Value, 0.001);
        view.Longest.ShouldAllBe(t => Math.Abs(t.ActiveHours + t.BlockedHours + t.ReworkHours - t.LeadHours) < 0.001);
    }

    // Mutation: flag only the range's longest dot.
    [Test]
    public void Build_WhenTasksEndInSeveralSprints_ShouldFlagOneLongestDotPerPeriodAndOverTwoDaysOver48Hours()
    {
        var view = Build([S3, S4], Task("SHIP-1", At(10, 6, 12), 48.04), Task("SHIP-2", At(10, 7, 12), 49), Task("SHIP-3", At(10, 20, 12), 5), Task("SHIP-4", At(10, 21, 12), 3));

        view.Periods.Select(p => p.Dots.Single(d => d.Longest).TaskKey).ShouldBe(["SHIP-2", "SHIP-3"]);
        view.Periods.SelectMany(p => p.Dots).ShouldAllBe(d => d.OverTwoDays == d.LeadHours > 48);
        view.Periods.SelectMany(p => p.Dots).Count(d => d.OverTwoDays).ShouldBe(1);
    }

    // Mutation: count a task with no start in the means.
    [Test]
    public void Build_WhenTaskHasNoStart_ShouldListItLeftOutAndCountCoverage()
    {
        var facts = new LeadTimeFactsBuilder().WithTask("SHIP-1").WithTask("SHIP-2").WithTask("SHIP-3", title: "Bulk import");
        TaskLeadTime[] tasks =
        [
            Task("SHIP-1", At(10, 6, 12), 10),
            Task("SHIP-2", At(10, 7, 12), 30, endedBy: LeadTimeEndedBy.Resolved),
            new("SHIP-3", null, At(10, 8, 12), LeadTimeEndedBy.Resolved, 0, 0, 0, 0, [], [], "Closed in one bulk edit with no start recorded"),
        ];

        var view = Build([S3], facts, tasks);

        view.LeftOut.Select(l => (l.Key, l.Title, l.Reason)).ShouldBe([("SHIP-3", "Bulk import", "Closed in one bulk edit with no start recorded")]);
        (view.Coverage.Tasks, view.Coverage.WithCommitTime, view.Coverage.Percent).ShouldBe((2, 1, 50));
        (view.Periods.Single().LeadTime.Tasks, view.Periods.Single().LeadTime.EndedAtResolved).ShouldBe((2, 1));
    }

    // Mutation: look the selected key up in the range instead of the period.
    [Test]
    public void Build_WhenTaskSelected_ShouldMeasureItFromItsStart()
    {
        var end = At(10, 7, 12);
        var start = end.AddHours(-10);
        var lead = new TaskLeadTime(
            "SHIP-1", start, end, LeadTimeEndedBy.LastCommit, 6, 0, 4, 2,
            [
                new LeadTimeSegment(InspectionSegmentKind.Rework, start.AddHours(6), end),
                new LeadTimeSegment(InspectionSegmentKind.Active, start, start.AddHours(6)),
                new LeadTimeSegment(InspectionSegmentKind.UnresolvedTail, end, end.AddHours(2)),
            ],
            [new LeadTimeEvent(InspectionEventKind.Commit, end, "abc1234")],
            null);
        var facts = new LeadTimeFactsBuilder().WithTask("SHIP-1").WithTask("SHIP-2");

        var view = Build([S3, S4], facts, [lead, Task("SHIP-2", At(10, 20, 12), 3)], period: S3, select: "SHIP-1");
        var other = Build([S3, S4], facts, [lead, Task("SHIP-2", At(10, 20, 12), 3)], period: S3, select: "SHIP-2");

        var selected = view.PeriodDetail.ShouldNotBeNull().Selected.ShouldNotBeNull();
        selected.Segments.Select(s => (s.Kind, s.FromHours, s.Hours, s.Counted))
            .ShouldBe([(InspectionSegmentKind.Active, 0d, 6d, true), (InspectionSegmentKind.Rework, 6d, 4d, true), (InspectionSegmentKind.UnresolvedTail, 10d, 2d, false)]);
        selected.Events.Single().Hours.ShouldBe(10d);
        other.PeriodDetail.ShouldNotBeNull().Selected.ShouldBeNull();
    }

    [Test]
    public void Build_WhenPeriodGiven_ShouldCountEachFeaturesTasksEndedHereBeforeAndInAll()
    {
        var facts = new LeadTimeFactsBuilder()
            .WithTask("SHIP-1", featureKey: "SHIP-99", featureTitle: "Coupons v2")
            .WithTask("SHIP-2", featureKey: "SHIP-99", featureTitle: "Coupons v2")
            .WithTask("SHIP-3", featureKey: "SHIP-99", featureTitle: "Coupons v2")
            .WithTask("SHIP-4", featureKey: "SHIP-99", featureTitle: "Coupons v2");
        TaskLeadTime[] tasks = [Task("SHIP-1", At(9, 22, 12), 24), Task("SHIP-2", At(10, 6, 12), 10), Task("SHIP-3", At(10, 8, 12), 30)];

        var view = Build([S2, S3], facts, tasks, period: S3);

        var feature = view.PeriodDetail.ShouldNotBeNull().Features.ShouldHaveSingleItem();
        (feature.Key, feature.Title, feature.TasksEnded, feature.TasksEndedBefore, feature.TasksTotal, feature.LongestTaskKey, feature.LeadTime.MeanHours)
            .ShouldBe(("SHIP-99", "Coupons v2", 2, 1, 4, "SHIP-3", 20d));
        feature.ExecutionLabel.ShouldBe("33 days so far");
    }
}
