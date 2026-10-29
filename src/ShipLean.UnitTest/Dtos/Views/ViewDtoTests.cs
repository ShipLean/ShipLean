using System.Text.Json;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
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
}
