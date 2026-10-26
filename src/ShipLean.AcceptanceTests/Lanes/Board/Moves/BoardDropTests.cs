using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Board.Moves;

// Dropping cards on the seeded Storefront stories board (SHIP), signed in as Joel. Each test moves a card of its own.
[TestFixture]
public class BoardDropTests : SeededAcceptanceTest
{
    private async Task<(BoardScreen Board, BoardMoves Moves)> OpenAsync()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Headers).ToHaveCountAsync(5);
        return (board, new BoardMoves(Page));
    }

    [Test]
    public async Task Board_WhenCardDroppedInColumn_ShouldSetMappedStateAndToast()
    {
        var (board, moves) = await OpenAsync();
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-progress");

        var moved = await moves.DragToCellAsync(board.CardTitled("Address autocomplete"), moves.Cell("Default lane", BoardMoves.InReviewColumn));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(moves.Toasts).ToContainTextAsync("SHIP-144 → In review (state Resolved)");
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-review");
        await Page.ReloadAsync();
        await Expect(board.Headers).ToHaveCountAsync(5);
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-review");
        await Expect(board.Count("In review")).ToContainTextAsync("1");
    }

    [Test]
    public async Task Board_WhenUnassignedCardPulled_ShouldAssignMover()
    {
        var (board, moves) = await OpenAsync();
        var card = board.CardTitled("Export sprint report to CSV");
        await Expect(BoardMoves.Assignee(card).First).ToHaveAttributeAsync("data-variant", "none");

        var moved = await moves.DragToCellAsync(card, moves.Cell("Default lane", BoardMoves.InProgressColumn));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(moves.Toasts.Filter(new LocatorFilterOptions { HasText = "Assigned to you" })).ToHaveCountAsync(1);
        await Page.ReloadAsync();
        await Expect(board.Headers).ToHaveCountAsync(5);
        var reloaded = board.CardTitled("Export sprint report to CSV");
        await Expect(board.CellOfTitled("Export sprint report to CSV")).ToHaveAttributeAsync("data-column", "col-in-progress");
        await Expect(BoardMoves.Assignee(reloaded).First).ToHaveAttributeAsync("title", "Joel Dickson");
    }
}
