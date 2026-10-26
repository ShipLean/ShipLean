using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Moves;

// One card moved twice in a row with Alt + arrow. Its own class: it moves a task the other move tests also use.
[TestFixture]
public class RepeatedTaskboardMoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Taskboard_WhenSameCardMovedTwiceByKeyboard_ShouldAcceptBothMoves()
    {
        var board = new SprintTaskboard(Page);
        var moves = new TaskboardMoves(Page);
        await board.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        var row = board.RowTitled("Pay with saved card");
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.ActiveColumn).GetByTestId(TaskboardTestIds.CardKey)).ToContainTextAsync(["SHIP-172"]);
        await moves.Card("SHIP-172").FocusAsync();

        var first = await moves.KeyMoveAsync("Alt+ArrowRight");
        await Expect(moves.Card("SHIP-172")).ToBeFocusedAsync();
        var second = await moves.KeyMoveAsync("Alt+ArrowRight");

        first.Status.ShouldBe(200);
        second.Status.ShouldBe(200, await second.TextAsync());
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.ClosedColumn).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-172" })).ToHaveCountAsync(1);
    }
}
