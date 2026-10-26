using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Board.Moves;

// A drop made against a revision a teammate has since moved on. Its own class: it moves a card Tom also moves.
[TestFixture]
public class BoardStaleMoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenMoveStale_ShouldSnapBackAndRefetch()
    {
        var board = new BoardScreen(Page);
        var moves = new BoardMoves(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-progress");

        // The page keeps the board it has until told otherwise: Tom's move never reaches it.
        var read = await Page.Context.APIRequest.GetAsync($"/api/boards/{SeedIds.Boards.ShipStories}");
        var snapshot = await read.TextAsync();
        var boardReads = 0;
        var serveSnapshot = true;
        await Page.RouteAsync(
            url => new Uri(url).AbsolutePath == $"/api/boards/{SeedIds.Boards.ShipStories}",
            async route =>
            {
                if (!serveSnapshot)
                {
                    await route.ContinueAsync();
                    return;
                }

                boardReads++;
                await route.FulfillAsync(new() { ContentType = "application/json", Body = snapshot });
            });
        await using var tom = await BoardTeammate.SignInAsTomAsync(Page, BaseUrl);
        await tom.MoveToColumnAsync(SeedIds.Boards.ShipStories, "SHIP-144", "col-in-review");
        var readsBefore = boardReads;

        var stale = await moves.DragToCellAsync(board.CardTitled("Address autocomplete"), moves.Cell("Default lane", BoardMoves.DoneColumn));

        stale.Status.ShouldBe(409);
        await Expect(moves.Toasts.Filter(new LocatorFilterOptions { HasText = "SHIP-144 was changed by someone else" })).ToHaveCountAsync(1);
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-progress");
        await Expect(board.Count("Done")).Not.ToHaveTextAsync("0");
        // The board was read again after the refusal; with the real answer it shows where Tom put the card.
        serveSnapshot = false;
        await Page.ReloadAsync();
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-review");
        boardReads.ShouldBeGreaterThan(readsBefore);
    }
}
