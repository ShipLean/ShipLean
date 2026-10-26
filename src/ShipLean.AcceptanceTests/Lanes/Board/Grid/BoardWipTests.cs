using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Board.Grid;

// WIP on the seeded Storefront stories board. The class lowers a limit, so it has its own database.
[TestFixture]
public class BoardWipTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenOverWip_ShouldTurnHeaderDangerNotBlock()
    {
        var board = new BoardScreen(Page);
        await BoardScreen.ChangeSettingsAsync(Page.Context, SeedIds.Boards.ShipStories, settings => BoardScreen.SetWipLimit(settings, "In progress", 1));

        await board.OpenAsync("SHIP", "stories");

        await Expect(board.Header("In progress")).ToHaveAttributeAsync("data-over", "true");
        await Expect(board.Header("Ready")).ToHaveAttributeAsync("data-over", "false");
        await Expect(board.Count("In progress")).ToContainTextAsync("/1");
        // Over the limit nothing is hidden or refused: the cards are all there.
        await Expect(board.Card("SHIP-141")).ToBeVisibleAsync();
    }
}
