using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Board.Touch;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Board.Touch;

// A card's ⋯, its actions sheet and Move to on a phone (SHIP-719 D2). The touch rules are checked on this task's regions
// only: the ⋯ buttons and the sheets. Each test moves a card of its own.
[TestFixture]
public class CardActionsTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private async Task<BoardScreen> OpenAsync(string level)
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", level);
        await new BoardPager(Page).WaitAsync();
        return board;
    }

    [Test]
    public async Task Card_WhenMovedToColumnFromSheetOnPhone_ShouldLandAtBottomWithMappedState()
    {
        var board = await OpenAsync("stories");
        var sheets = new CardActionsSheet(Page);
        var card = board.CardTitled("Address autocomplete");
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-progress");
        (await TouchRules.ViolationsAsync(Page, CardActionsSheet.More(card))).ShouldBeEmpty();

        await CardActionsSheet.More(card).TapAsync();
        await Expect(sheets.ActionsFor("SHIP-144")).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, sheets.ActionsFor("SHIP-144"))).ShouldBeEmpty();
        await CardActionsSheet.Row(sheets.ActionsFor("SHIP-144"), "Move to").TapAsync();
        await Expect(sheets.MoveToSheet).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, sheets.MoveToSheet)).ShouldBeEmpty();
        var moved = await Page.RunAndWaitForResponseAsync(
            () => CardActionsSheet.Row(sheets.MoveToSheet, "In review").TapAsync(),
            r => r.Request.Method == "POST" && r.Url.Contains("/moves"));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var request = moved.Request.PostData!;
        request.ShouldContain("\"beforeItemId\":null");
        await Expect(new BoardMoves(Page).Toasts).ToContainTextAsync("SHIP-144 → In review (state Resolved)");
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-review");
        await Page.ReloadAsync();
        await new BoardPager(Page).WaitAsync();
        await Expect(board.CellOfTitled("Address autocomplete")).ToHaveAttributeAsync("data-column", "col-in-review");
    }

    [Test]
    public async Task Feature_WhenMovedToDoneWithOpenChildrenOnPhone_ShouldAskFirst()
    {
        var board = await OpenAsync("features");
        var sheets = new CardActionsSheet(Page);
        var confirm = new BoardDoneConfirm(Page);
        var card = board.CardTitled("Checkout v2");
        await Expect(board.CellOfTitled("Checkout v2")).ToHaveAttributeAsync("data-column", "col-in-progress");
        var moves = confirm.CountMoves();

        await CardActionsSheet.More(card).TapAsync();
        await CardActionsSheet.Row(sheets.ActionsFor("SHIP-101"), "Move to").TapAsync();
        await CardActionsSheet.Row(sheets.MoveToSheet, "Done").TapAsync();

        await Expect(confirm.Dialog).ToBeVisibleAsync();
        await Expect(confirm.Dialog).ToContainTextAsync("Checkout v2");
        moves().ShouldBe(0);

        var moved = await confirm.ClickAndWaitForMoveAsync(confirm.MoveAnyway);

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(board.CellOfTitled("Checkout v2")).ToHaveAttributeAsync("data-column", "col-done");
    }
}
