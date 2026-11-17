using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Dtos.Views;

[TestFixture]
public class ViewDtoTests
{
    [Test]
    public void BoardViewDto_WhenStoriesBoardRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BoardViewDto>(BoardDtoFixture.Read("views", "board-view.stories.json")).ShouldBeTrue();
    }

    [Test]
    public void BoardViewDto_WhenFeaturesBoardRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BoardViewDto>(BoardDtoFixture.Read("views", "board-view.features.json")).ShouldBeTrue();
    }

    [Test]
    public void TaskboardViewDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<TaskboardViewDto>(BoardDtoFixture.Read("views", "taskboard-view.json")).ShouldBeTrue();
    }

    [Test]
    public void FeatureBoardViewDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<FeatureBoardViewDto>(BoardDtoFixture.Read("views", "feature-board-view.json")).ShouldBeTrue();
    }

    [Test]
    public void BacklogViewDto_WhenTreeRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BacklogViewDto>(BoardDtoFixture.Read("views", "backlog-view.tree.json")).ShouldBeTrue();
    }

    [Test]
    public void BacklogTreeFixture_ShouldEndWithTheNoParentGroupAndCarryNoForecastCopy()
    {
        var backlog = BoardDtoFixture.Read("views", "backlog-view.tree.json").Deserialize<BacklogViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        backlog.Rows.Single(r => r.Kind == BacklogRowKind.NoParentGroup).Type.ShouldBeNull();
        backlog.Rows.Where(r => r.Kind != BacklogRowKind.NoParentGroup).ShouldAllBe(r => r.Type != null && r.State != null);
        backlog.Forecast.ShouldBeNull();
        backlog.Velocity.ShouldBeNull();
    }

    [Test]
    public void StoriesBoardFixture_ShouldShowTheOverLimitAndOlderDoneCases()
    {
        var board = BoardDtoFixture.Read("views", "board-view.stories.json").Deserialize<BoardViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        board.Columns.Single(c => c.Id == "col-in-review").OverLimit.ShouldBeTrue();
        board.Lanes.SelectMany(l => l.Cells).Single(c => c.OlderCount > 0).OlderCursor.ShouldNotBeNull();
        board.Lanes.SelectMany(l => l.Cells).SelectMany(c => c.Cards).Select(c => c.StyleRuleId).ShouldContain("rule-blocked");
        board.Columns.Sum(c => c.Count).ShouldBe(board.Lanes.Sum(l => l.CardCount));
        board.Columns.ShouldContain(c => c.HiddenCount > 0);
        board.StyleRules.Select(r => r.Id).ShouldContain("rule-blocked");
    }

    [Test]
    public void FeaturesBoardFixture_ShouldEndWithTheNoEpicLaneAndKeepEmptyEpicLanes()
    {
        var board = BoardDtoFixture.Read("views", "board-view.features.json").Deserialize<BoardViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        board.Lanes[^1].Kind.ShouldBe(BoardLaneKind.NoEpic);
        board.Lanes.Take(4).ShouldAllBe(l => l.Kind == BoardLaneKind.Epic && l.Epic != null && l.AcceptsDrops);
        board.Lanes.SelectMany(l => l.Cells).SelectMany(c => c.Cards).ShouldContain(c => c.SumWarn);
    }

    [Test]
    public void TaskboardFixture_ShouldEndWithTheUnparentedRowAndStartPeopleWithUnassigned()
    {
        var board = BoardDtoFixture.Read("views", "taskboard-view.json").Deserialize<TaskboardViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        board.Rows[^1].Parent.ShouldBeNull();
        board.PeopleRows[0].Person.ShouldBeNull();
        board.Rows.ShouldAllBe(r => r.Cells.Count == board.Columns.Count);
        board.Rows.Sum(r => r.Cells.Sum(c => c.Tasks.Count)).ShouldBe(board.PeopleRows.Sum(r => r.TaskCount));
        board.ParentStates.Select(s => s.Type).ShouldBe([WorkItemTypeId.Story, WorkItemTypeId.Bug]);
        board.Rows.ShouldContain(r => r.OfferResolve);
    }

    [Test]
    public void FeatureBoardFixture_ShouldHaveNoUnparentedRowAndSectionsMatchingRows()
    {
        var board = BoardDtoFixture.Read("views", "feature-board-view.json").Deserialize<FeatureBoardViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        board.Sections.Select(s => s.Depth).ShouldBe([0, 1, 2]);
        board.Sections[2].ParentFeature.ShouldNotBeNull().Id.ShouldBe(board.Sections[1].Id);
        board.Rows.Select(r => r.FeatureId).Distinct().ShouldBe(board.Sections.Select(s => s.Id));
        board.Rows.ShouldAllBe(r => r.Cells.Count == board.Columns.Count);
        board.Rows.Sum(r => r.Cells.Sum(c => c.Tasks.Count)).ShouldBe(board.PeopleRows.Sum(r => r.TaskCount));
        board.PeopleRows[0].Person.ShouldBeNull();
        board.Header.StoriesTotal.ShouldBe(board.Rows.Count);
        board.Header.TasksTotal.ShouldBe(board.Columns.Sum(c => c.Count));
        board.Header.AnyInBacklog.ShouldBe(board.Rows.Any(r => r.Sprint is null));
        board.Rows.ShouldContain(r => r.OfferResolve);
    }

    [Test]
    public void InspectionMetricsView_ShouldSurviveRoundTrip()
    {
        BoardDtoFixture.SurvivesRoundTrip<InspectionMetricsViewDto>(BoardDtoFixture.Read("views", "inspection-metrics-view.json")).ShouldBeTrue();
    }

    [Test]
    public void InspectionMetricsView_ShouldShowEveryCase()
    {
        var view = BoardDtoFixture.Read("views", "inspection-metrics-view.json").Deserialize<InspectionMetricsViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        view.Periods.Count(p => !p.InProgress).ShouldBe(3);
        view.Periods.Count(p => p.InProgress).ShouldBe(1);
        view.Longest.Count.ShouldBe(5);
        view.Longest.Count(t => t.EndedBy == LeadTimeEndedBy.Resolved).ShouldBe(1);
        view.Coverage.Percent.ShouldNotBeNull().ShouldBeLessThan(100);
        view.Quarters.Count.ShouldBe(2);
        view.Cut.ShouldBe(InspectionCut.Feature);
        view.Cuts.ShouldNotBeNull().ShouldAllBe(row => row.Cells.Count == view.Periods.Count);
        view.LeftOut.ShouldHaveSingleItem();
        var detail = view.PeriodDetail.ShouldNotBeNull();
        detail.Tasks.ShouldNotBeEmpty();
        detail.Features.ShouldNotBeEmpty();
        var selected = detail.Selected.ShouldNotBeNull();
        selected.Segments.Select(s => s.Kind).ShouldBe([InspectionSegmentKind.Active, InspectionSegmentKind.Rework, InspectionSegmentKind.UnresolvedTail]);
        selected.Events.Select(e => e.Kind).Distinct().Count().ShouldBe(Enum.GetValues<InspectionEventKind>().Length);
        detail.Previous.ShouldNotBeNull();
        detail.Next.ShouldNotBeNull();
        view.Headline.QuarterToDate.ShouldNotBeNull().ToDate.ShouldBeTrue();
        view.Headline.LastQuarter.ShouldNotBeNull().ToDate.ShouldBeFalse();
        var quality = view.Quality.ShouldNotBeNull();
        view.Periods.ShouldAllBe(p => p.Quality != null);
        view.Periods.ShouldContain(p => p.Quality!.MergedPrs == null && p.Quality.Per100Prs == null);
        view.Quarters.ShouldAllBe(q => q.Quality != null);
        var bugs = detail.Bugs.ShouldNotBeNull();
        bugs.ShouldContain(b => b.CausedByKey != null && b.CausedByTitle != null);
        bugs.ShouldContain(b => b.FoundBeforeRelease);
        bugs.ShouldContain(b => b.Severity == null);
        detail.Quality.ShouldNotBeNull().FoundBeforeRelease.ShouldBe(bugs.Count(b => b.FoundBeforeRelease));
        var range = view.Bugs.ShouldNotBeNull();
        range.Count.ShouldBeGreaterThan(bugs.Count);
        range.ShouldAllBe(b => view.Periods.Any(p => p.Id == b.PeriodId));
        var split = quality.EscapingBySeverity;
        (split.Sev1 + split.Sev2 + split.Sev3 + split.Sev4 + split.Unset).ShouldBe(quality.Escaping);
        IEnumerable<LeadTimeStatsDto> stats =
        [
            .. view.Periods.Select(p => p.LeadTime), .. view.Quarters.Select(q => q.LeadTime), .. view.Cuts.SelectMany(c => c.Cells.Append(c.All)),
            .. detail.Features.Select(f => f.LeadTime), detail.LeadTime,
        ];
        foreach (var s in stats.Where(s => s.MeanHours is not null))
        {
            (s.MeanActiveHours + s.MeanBlockedHours + s.MeanReworkHours).ShouldNotBeNull().ShouldBe(s.MeanHours!.Value, 0.001);
        }
    }

    // SHIP-1398 CK2: one period from before the store kept runs, one with no merged PR, a task by a person, a task with no Feature.
    [Test]
    public void InspectionMetricsView_ShouldShowEveryCostCase()
    {
        var view = BoardDtoFixture.Read("views", "inspection-metrics-view.json").Deserialize<InspectionMetricsViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        var cost = view.Cost.ShouldNotBeNull();
        cost.Available.ShouldBeTrue();
        cost.Periods.Select(p => p.PeriodId).ShouldBe(view.Periods.Select(p => p.Id));
        cost.Quarters.Select(q => q.Quarter).ShouldBe(view.Quarters.Select(q => q.Quarter));
        cost.Periods.ShouldContain(p => !p.Stats.Kept && p.Stats.TokensPerTask == null && p.Stats.TokensPerTaskLabel == "—");
        cost.Periods.ShouldContain(p => p.Stats.Kept && p.Stats.MergedPrs == null && p.Stats.TokensPerMergedPr == null);
        cost.Periods.ShouldContain(p => p.Stats.ChangePctVsPrevious != null);
        cost.Range.ShouldNotBeNull().TasksWithRuns.ShouldBeLessThan(cost.Range.Tasks);
        cost.Headline.Finished.ShouldNotBeNull();
        cost.Headline.LastQuarter.ShouldNotBeNull().Kept.ShouldBeFalse();
        cost.MostTokens.Select(t => t.Tokens).ShouldBeInOrder(SortDirection.Descending);
        cost.MostTokens.ShouldContain(t => !t.Agent);
        cost.MostTokens.ShouldContain(t => t.FeatureKey == null);
        cost.ByFeature.ShouldContain(f => f.Key == null);
    }

    [Test]
    public void FeatureTimelineView_ShouldSurviveRoundTrip()
    {
        BoardDtoFixture.SurvivesRoundTrip<FeatureTimelineViewDto>(BoardDtoFixture.Read("views", "feature-timeline-view.json")).ShouldBeTrue();
    }

    [Test]
    public void FeatureTimelineRunningView_ShouldSurviveRoundTrip()
    {
        BoardDtoFixture.SurvivesRoundTrip<FeatureTimelineViewDto>(BoardDtoFixture.Read("views", "feature-timeline-running-view.json")).ShouldBeTrue();
    }

    [Test]
    public void FeatureTimelineView_ShouldSendEnumsAsCamelCase()
    {
        var finished = BoardDtoFixture.Read("views", "feature-timeline-view.json").Deserialize<FeatureTimelineViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();
        var running = BoardDtoFixture.Read("views", "feature-timeline-running-view.json").Deserialize<FeatureTimelineViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        var finishedJson = JsonSerializer.SerializeToNode(finished, BoardDtoFixture.Options)!;
        var runningJson = JsonSerializer.SerializeToNode(running, BoardDtoFixture.Options)!;

        finishedJson["keyDates"]![2]!["point"]!.GetValue<string>().ShouldBe("executionStarted");
        finishedJson["bars"]!.AsArray().SelectMany(b => b!["reviews"]!.AsArray()).Select(r => r!["verdict"]!.GetValue<string>()).ShouldContain("changesRequested");
        finishedJson["bars"]!.AsArray().SelectMany(b => b!["history"]!.AsArray()).Select(h => h!["state"]!.GetValue<string>()).ShouldContain("blocked");
        finishedJson["edges"]!.AsArray().Select(e => e!["source"]!.GetValue<string>()).ShouldContain("wave");
        runningJson["feature"]!["stateCategory"]!.GetValue<string>().ShouldBe("inProgress");
        runningJson["bars"]!.AsArray().Select(b => b!["state"]!.GetValue<string>()).ShouldContain("inFlight");
    }

    [Test]
    public void FeatureTimelineView_WithMcpOptions_ShouldMatchMcpFixture()
    {
        var view = BoardDtoFixture.Read("views", "feature-timeline-view.json").Deserialize<FeatureTimelineViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        var mcp = JsonSerializer.SerializeToNode(view, TimelineJson.McpOptions);

        JsonNode.DeepEquals(mcp, BoardDtoFixture.Read("views", "feature-timeline-mcp.json")).ShouldBeTrue(mcp!.ToJsonString());
    }

    [Test]
    public void FeatureTimelineFixtures_ShouldShowEveryCase()
    {
        var finished = BoardDtoFixture.Read("views", "feature-timeline-view.json").Deserialize<FeatureTimelineViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();
        var running = BoardDtoFixture.Read("views", "feature-timeline-running-view.json").Deserialize<FeatureTimelineViewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        finished.Finished.ShouldBeTrue();
        finished.KeyDates.Select(k => k.Point).ShouldBe([KeyDatePoint.Added, KeyDatePoint.Planned, KeyDatePoint.ExecutionStarted, KeyDatePoint.Finished]);
        finished.Gaps.Count.ShouldBe(3);
        finished.Scale.NowHours.ShouldBeNull();
        finished.Scale.Days.ShouldContain(d => d.Weekend);
        finished.Scale.Sprints.ShouldNotBeEmpty();
        finished.CriticalPath.ShouldNotBeEmpty();
        finished.Bars.Where(b => b.OnCriticalPath).Select(b => b.Key).ShouldBe(finished.CriticalPath, ignoreOrder: true);
        finished.Bars.Single(b => b.Key == "SHIP-194").History.ShouldContain(h => h.State == TimelinePeriodState.Blocked);
        finished.Bars.Single(b => b.Key == "SHIP-192").Reviews.Select(r => r.Verdict).ShouldBe([ReviewVerdict.ChangesRequested, ReviewVerdict.Approved]);
        var diamond = finished.Bars.Single(b => b.Key == "SHIP-198");
        diamond.Inferred.ShouldBeTrue();
        diamond.Start.ShouldBeNull();
        diamond.End.ShouldNotBeNull();
        var blocked = finished.Edges.Single(e => e.Kind == TimelineEdgeKind.Blocked);
        (blocked.From, blocked.To).ShouldBe(("SHIP-199", "SHIP-196"));
        blocked.UnblockedAt.ShouldNotBeNull();
        finished.Edges.ShouldContain(e => e.Source == TimelineEdgeSource.Wave && e.Kind == TimelineEdgeKind.Depends);
        finished.Edges.ShouldContain(e => e.WaitLabel != null);
        finished.Edges.Where(e => e.WaitLabel != null).ShouldAllBe(e => e.WaitHours >= 12);

        running.Finished.ShouldBeFalse();
        running.Scale.NowHours.ShouldNotBeNull();
        running.CriticalPath.ShouldBeEmpty();
        running.KeyDates[3].At.ShouldBeNull();
        running.Gaps[^1].SoFar.ShouldBeTrue();
        running.Bars.ShouldContain(b => b.State == TimelineBarState.InFlight && b.End == null);
        running.NotStarted.ShouldNotBeEmpty();
    }
}
