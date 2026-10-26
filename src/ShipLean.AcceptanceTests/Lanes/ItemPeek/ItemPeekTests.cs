using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.ItemPeek;

namespace ShipLean.AcceptanceTests.Lanes.ItemPeek;

// The work item modal over a seeded screen. Reads only.
[TestFixture]
public class ItemPeekTests : SeededAcceptanceTest
{
    [Test]
    public async Task ItemPeek_WhenBoardUrlHasItem_ShouldShowItemOverBoard()
    {
        var board = new BoardScreen(Page);
        var peek = new ItemPeekDialog(Page);

        await Page.GotoAsync(Links.WithItem(Links.Board("SHIP", "stories"), "SHIP-141"));

        await peek.WaitOpenAsync("SHIP-141");
        await Expect(peek.Title).ToHaveValueAsync("Pay with saved card");
        await Expect(board.View).ToBeAttachedAsync();

        await Page.Keyboard.PressAsync("Escape");

        await peek.WaitClosedAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/boards/stories$"));
        await Expect(board.View).ToBeVisibleAsync();
    }

    [Test]
    public async Task ItemPeek_WhenOpenFullPageClicked_ShouldNavigateToItemPage()
    {
        var peek = new ItemPeekDialog(Page);
        await Page.GotoAsync(Links.WithItem(Links.Board("SHIP", "stories"), "SHIP-141"));
        await peek.WaitOpenAsync("SHIP-141");

        await peek.OpenFullPage.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/items/SHIP-141$"));
        await peek.WaitClosedAsync();
    }
}
