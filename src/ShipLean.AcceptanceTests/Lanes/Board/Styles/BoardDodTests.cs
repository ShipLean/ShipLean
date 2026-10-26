using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;

namespace ShipLean.AcceptanceTests.Lanes.Board.Styles;

// The Definition of done popover on the seeded Storefront stories board.
[TestFixture]
public class BoardDodTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenDodOpened_ShouldShowStatePerType()
    {
        var board = new BoardScreen(Page);
        var dod = new BoardDod(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Headers).ToHaveCountAsync(5);

        await BoardDod.Open(board.Header("Ready")).ClickAsync();

        await Expect(dod.Popover).ToBeVisibleAsync();
        await Expect(dod.Types).ToContainTextAsync(["User Story", "Bug"]);
        await Expect(dod.States).ToContainTextAsync(["New", "Triaged"]);
        await Expect(dod.Body).ToContainTextAsync("Acceptance criteria written and agreed");
        await Expect(dod.Body).ToContainTextAsync("Estimated in story points");
        await Expect(dod.Body).ToContainTextAsync("No open questions for the product owner");
    }
}
