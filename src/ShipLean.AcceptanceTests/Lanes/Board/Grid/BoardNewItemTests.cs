using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;

namespace ShipLean.AcceptanceTests.Lanes.Board.Grid;

// "+ New" on the stories board adds a work item; this class creates one, so it has its own database.
[TestFixture]
public class BoardNewItemTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenNewItemAdded_ShouldAppearInNew()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Headers).ToHaveCountAsync(5);
        var before = await board.Count("New").InnerTextAsync();

        await board.AddCardAsync("Offline mode for the dashboard");

        await Expect(board.CardTitled("Offline mode for the dashboard")).ToBeVisibleAsync();
        await Expect(board.CellOfTitled("Offline mode for the dashboard")).ToHaveAttributeAsync("data-column", "col-new");
        // The default lane, not the first (Expedite) lane.
        await Expect(BoardScreen.LaneName(board.LaneOfTitled("Offline mode for the dashboard"))).ToHaveTextAsync("Default lane");
        await Expect(board.Count("New")).Not.ToHaveTextAsync(before);
    }
}
