using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Board.Moves;

// A teammate's move reaches the open board. Its own class: it moves a card Tom moves.
[TestFixture]
public class BoardTeammateMoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenTeammateMoves_ShouldSlideCard()
    {
        var board = new BoardScreen(Page);
        var moves = new BoardMoves(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.CellOfTitled("Sign in with passkey")).ToHaveAttributeAsync("data-column", "col-in-progress");
        await using var tom = await BoardTeammate.SignInAsTomAsync(Page, BaseUrl);

        await tom.MoveToColumnAsync(SeedIds.Boards.ShipStories, "SHIP-129", "col-in-review");

        await Expect(board.CellOfTitled("Sign in with passkey")).ToHaveAttributeAsync("data-column", "col-in-review");
        await Expect(moves.Toasts).ToContainTextAsync("Tom Okafor moved SHIP-129 to Resolved");
        await Expect(board.Count("In review")).ToContainTextAsync("1");
    }
}
