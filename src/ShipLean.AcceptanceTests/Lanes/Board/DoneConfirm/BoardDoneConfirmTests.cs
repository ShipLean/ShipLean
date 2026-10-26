using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;

namespace ShipLean.AcceptanceTests.Lanes.Board.DoneConfirm;

// Dragging Checkout v2 (SHIP-101, three stories and bugs still open) into Done on the seeded Features board.
[TestFixture]
public class BoardDoneConfirmTests : SeededAcceptanceTest
{
    [Test]
    public async Task FeaturesBoard_WhenMovedToDoneWithOpenChildren_ShouldAskFirst()
    {
        var board = new BoardScreen(Page);
        var confirm = new BoardDoneConfirm(Page);
        await board.OpenAsync("SHIP", "features");
        await Expect(board.Headers).ToHaveCountAsync(5);
        var card = board.CardTitled("Checkout v2");
        await Expect(board.CellOfTitled("Checkout v2")).ToHaveAttributeAsync("data-column", "col-in-progress");
        var moves = confirm.CountMoves();

        await confirm.DragToDoneAsync(card);

        await Expect(confirm.Dialog).ToContainTextAsync("Checkout v2");
        await Expect(confirm.OpenChildren).ToHaveCountAsync(4);
        await Expect(confirm.Dialog).ToContainTextAsync("Address autocomplete");
        await Expect(confirm.Dialog).Not.ToContainTextAsync("Order confirmation email");
        await Expect(confirm.Dialog).Not.ToContainTextAsync("Totals wrong when coupon removed");
        moves().ShouldBe(0);

        await confirm.Cancel.ClickAsync();

        await Expect(confirm.Dialog).ToHaveCountAsync(0);
        await Expect(board.CellOfTitled("Checkout v2")).ToHaveAttributeAsync("data-column", "col-in-progress");
        moves().ShouldBe(0);

        await confirm.DragToDoneAsync(board.CardTitled("Checkout v2"));
        await Expect(confirm.Dialog).ToBeVisibleAsync();
        await Expect(confirm.MoveAnyway).ToBeFocusedAsync();
        var moved = await confirm.ClickAndWaitForMoveAsync(confirm.MoveAnyway);

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(board.CellOfTitled("Checkout v2")).ToHaveAttributeAsync("data-column", "col-done");
        await Page.ReloadAsync();
        await Expect(board.Headers).ToHaveCountAsync(5);
        await Expect(board.CellOfTitled("Checkout v2")).ToHaveAttributeAsync("data-column", "col-done");
    }
}
