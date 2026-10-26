using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Board.Peek;

// The seeded Storefront boards: a card title or a roll-up child opens the item in the modal over the board. Reads only.
[TestFixture]
public class BoardPeekTests : SeededAcceptanceTest
{
    [Test]
    public async Task BoardPeek_WhenCardTitleClicked_ShouldOpenItemOverBoard()
    {
        var board = new BoardScreen(Page);
        var peek = new ItemPeekDialog(Page);
        await board.OpenAsync("SHIP", "stories");
        var card = board.Card("SHIP-141");
        var title = await card.GetByTestId(BoardTestIds.CardTitle).InnerTextAsync();

        await card.GetByTestId(BoardTestIds.CardTitle).ClickAsync();

        await peek.WaitOpenAsync("SHIP-141");
        await Expect(peek.Title).ToHaveValueAsync(title);
        await Expect(board.View).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"item=SHIP-141$"));

        await Page.GoBackAsync();

        await peek.WaitClosedAsync();
        await Expect(board.View).ToBeVisibleAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex("item="));
    }

    [Test]
    public async Task BoardPeek_WhenChildClicked_ShouldOpenChildInModal()
    {
        var board = new BoardScreen(Page);
        var children = new BoardChildren(Page);
        var peek = new ItemPeekDialog(Page);
        await board.OpenAsync("SHIP", "features");
        await children.OpenAsync(board.Card("SHIP-101"), "S");

        await children.Row("SHIP-141").GetByTestId(ItemPeekTestIds.Link).ClickAsync();

        await peek.WaitOpenAsync("SHIP-141");
        await Expect(board.View).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"item=SHIP-141$"));
    }
}
