using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.FeatureBoard;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.FeatureBoard;

// Checkout v2 (SHIP-101) on the seeded Storefront project, signed in as Joel: its stories sit in sprint 24 and in the
// Backlog. The journey runs in order on one Feature: open the board from the form, move a task to another column and to
// a story in another sprint (its own sprint unchanged), add a task, then group by People.
[TestFixture]
public class FeatureBoardJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string Feature = "SHIP-101";
    private const string PaySavedCard = "Pay with saved card";
    private const string GiftCard = "Gift card balance check";

    [Test]
    [Order(1)]
    public async Task FeatureBoard_WhenOpenedFromFeature_ShouldShowStoriesFromEverySprint()
    {
        var feature = new FeatureBoardPage(Page);
        await feature.OpenFeatureAsync(Project, Feature);

        await feature.FollowTaskBoardLinkAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"{Links.WorkItem(Project, Feature)}/board$"));
        await Expect(feature.Page).ToBeVisibleAsync();
        await Expect(feature.Header).ToContainTextAsync("Checkout v2");
        await Expect(FeatureBoardPage.SprintPill(feature.Board.RowTitled(PaySavedCard))).ToHaveAttributeAsync("data-sprint", "current");
        await Expect(FeatureBoardPage.SprintPill(feature.Board.RowTitled(GiftCard))).ToHaveAttributeAsync("data-sprint", "backlog");
    }

    [Test]
    [Order(2)]
    public async Task FeatureBoard_WhenTaskDragged_ShouldSetStateAndParentButKeepSprint()
    {
        var feature = new FeatureBoardPage(Page);
        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await feature.Board.Grid.WaitForAsync();
        var payRow = feature.Board.RowTitled(PaySavedCard);
        var sprintBefore = await feature.SprintOfAsync(Project, "SHIP-173");
        sprintBefore.ShouldNotBeNull();

        var toColumn = await feature.Moves.DragToCellAsync(feature.Moves.Card("SHIP-172"), TaskboardMoves.Cell(payRow, TaskboardMoves.InReviewColumn));

        toColumn.Ok.ShouldBeTrue(await toColumn.TextAsync());
        await Expect(TaskboardMoves.Cell(payRow, TaskboardMoves.InReviewColumn).GetByTestId(TaskboardTestIds.CardKey)).ToContainTextAsync(["SHIP-171", "SHIP-172"]);

        var toStory = await feature.Moves.DragToCellAsync(
            feature.Moves.Card("SHIP-173"),
            TaskboardMoves.Cell(feature.Board.RowTitled(GiftCard), TaskboardMoves.NewColumn));

        toStory.Ok.ShouldBeTrue(await toStory.TextAsync());
        await Expect(feature.Board.RowTitled(GiftCard).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-173" })).ToHaveCountAsync(1);
        await Page.ReloadAsync();
        await feature.Board.Grid.WaitForAsync();
        await Expect(feature.Board.RowTitled(GiftCard).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-173" })).ToHaveCountAsync(1);
        await Expect(feature.Board.RowTitled(PaySavedCard).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-173" })).ToHaveCountAsync(0);
        (await feature.SprintOfAsync(Project, "SHIP-173")).ShouldBe(sprintBefore);
    }

    [Test]
    [Order(3)]
    public async Task FeatureBoard_WhenTaskAdded_ShouldAppearUnderItsStory()
    {
        var feature = new FeatureBoardPage(Page);
        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await feature.Board.Grid.WaitForAsync();
        var story = feature.Board.RowTitled(PaySavedCard);
        var before = await story.GetByTestId(TaskboardTestIds.Card).CountAsync();

        await story.GetByTestId(TaskboardTestIds.AddTask).ClickAsync();
        var created = await feature.Board.AddTaskAsync("Write the saved card docs");

        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(story.GetByTestId(TaskboardTestIds.Card)).ToHaveCountAsync(before + 1);
        await Expect(story).ToContainTextAsync("Write the saved card docs");
    }

    [Test]
    [Order(4)]
    public async Task FeatureBoard_WhenGroupedByPeople_ShouldShowOneGridOfPeople()
    {
        var feature = new FeatureBoardPage(Page);
        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await feature.Board.Grid.WaitForAsync();

        var saved = await feature.Board.GroupByAsync("People");

        saved.Ok.ShouldBeTrue();
        await Expect(feature.Board.Rows.First).ToHaveAttributeAsync("data-row", "unassigned");
        await Expect(Page.GetByTestId(TaskboardTestIds.RowState)).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(TaskboardTestIds.AddTask)).ToHaveCountAsync(0);
        await Expect(feature.Board.Cards.Filter(new() { HasText = "Write the saved card docs" })).ToHaveCountAsync(1);
    }
}
