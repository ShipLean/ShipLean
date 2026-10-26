using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Grid;

// Sprint 24's Taskboard on the seeded Storefront project (SHIP), signed in as Joel. The tests run in order: the
// create comes before the Group by switch, which is saved for the user.
[TestFixture]
public class TaskboardGridTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task Taskboard_WhenStoryCompleted_ShouldStartCollapsed()
    {
        var board = new SprintTaskboard(Page);
        await board.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);

        var closedBug = board.RowTitled("Totals wrong when coupon removed");
        await Expect(closedBug).ToHaveAttributeAsync("data-collapsed", "true");
        await Expect(closedBug.GetByTestId(TaskboardTestIds.Card)).ToHaveCountAsync(0);
        var openStory = board.RowTitled("Pay with saved card");
        await Expect(openStory).ToHaveAttributeAsync("data-collapsed", "false");
        await Expect(openStory.GetByTestId(TaskboardTestIds.Card).First).ToBeVisibleAsync();
        await Expect(board.Rows.Last).ToHaveAttributeAsync("data-row", "unparented");

        await closedBug.GetByTestId(TaskboardTestIds.RowToggle).ClickAsync();
        await Expect(closedBug).ToHaveAttributeAsync("data-collapsed", "false");
        await Expect(closedBug.GetByTestId(TaskboardTestIds.Card).First).ToBeVisibleAsync();
    }

    [Test]
    [Order(2)]
    public async Task Taskboard_WhenTaskAdded_ShouldKeepBoxOpen()
    {
        var board = new SprintTaskboard(Page);
        await board.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);

        var story = board.RowTitled("Pay with saved card");
        var before = await story.GetByTestId(TaskboardTestIds.Card).CountAsync();
        await story.GetByTestId(TaskboardTestIds.AddTask).ClickAsync();
        var created = await board.AddTaskAsync("Write the saved card docs");
        created.Ok.ShouldBeTrue();

        await Expect(story.GetByTestId(TaskboardTestIds.Card)).ToHaveCountAsync(before + 1);
        await Expect(story).ToContainTextAsync("Write the saved card docs");
        await Expect(board.TaskTitle).ToBeVisibleAsync();
        await Expect(board.TaskTitle).ToHaveValueAsync("");

        var second = await board.AddTaskAsync("And the changelog entry");
        second.Ok.ShouldBeTrue();
        await Expect(story.GetByTestId(TaskboardTestIds.Card)).ToHaveCountAsync(before + 2);
    }

    [Test]
    [Order(3)]
    public async Task Taskboard_WhenGroupedByPeople_ShouldShowCountsOnly()
    {
        var board = new SprintTaskboard(Page);
        await board.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);

        var saved = await board.GroupByAsync("People");
        saved.Ok.ShouldBeTrue();

        await Expect(board.Rows.First).ToHaveAttributeAsync("data-row", "unassigned");
        await Expect(board.Rows.First.GetByTestId(TaskboardTestIds.RowTitle)).ToHaveTextAsync("Unassigned");
        await Expect(board.Rows.Nth(1).GetByTestId(TaskboardTestIds.RowCount)).ToHaveTextAsync(new Regex(@"^\d+ tasks?$"));
        await Expect(Page.GetByTestId(TaskboardTestIds.RowState)).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(TaskboardTestIds.AddTask)).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(TaskboardTestIds.RowProgress)).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await board.Grid.WaitForAsync();
        await Expect(board.Rows.First).ToHaveAttributeAsync("data-row", "unassigned");
    }
}
