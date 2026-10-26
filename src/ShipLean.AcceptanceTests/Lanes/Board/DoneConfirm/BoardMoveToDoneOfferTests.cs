using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Board.DoneConfirm;

// Ops hygiene (SHIP-104) has three stories, two open (SHIP-162, SHIP-163); Tom closes them on the Stories board behind the page's back.
[TestFixture]
public class BoardMoveToDoneOfferTests : SeededAcceptanceTest
{
    [Test]
    public async Task FeaturesBoard_WhenLastChildCloses_ShouldOfferMoveToDone()
    {
        var board = new BoardScreen(Page);
        var confirm = new BoardDoneConfirm(Page);
        await board.OpenAsync("SHIP", "features");
        await Expect(board.Headers).ToHaveCountAsync(5);
        var card = board.CardTitled("Ops hygiene");
        await Expect(card).ToHaveCountAsync(1);
        await using var tom = await BoardTeammate.SignInAsTomAsync(Page, BaseUrl);

        await tom.MoveToColumnAsync(SeedIds.Boards.ShipStories, "SHIP-162", "col-done");

        // SHIP-131 was already closed, so the card now reads 2/3 (waiting for that also waits for Tom's move to reach the
        // page). One story is still open: nothing is offered.
        await Expect(card.GetByTestId(BoardTestIds.CardRollup).First).ToContainTextAsync("2/3");
        await Expect(BoardDoneConfirm.Offer(card)).ToHaveCountAsync(0);

        await tom.MoveToColumnAsync(SeedIds.Boards.ShipStories, "SHIP-163", "col-done");

        await Expect(BoardDoneConfirm.Offer(card)).ToContainTextAsync("All 3 closed");
        await Expect(board.CellOfTitled("Ops hygiene")).ToHaveAttributeAsync("data-column", "col-new");

        var moved = await confirm.ClickAndWaitForMoveAsync(BoardDoneConfirm.MoveToDone(card));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(confirm.Dialog).ToHaveCountAsync(0);
        await Expect(board.CellOfTitled("Ops hygiene")).ToHaveAttributeAsync("data-column", "col-done");
        await Expect(BoardDoneConfirm.Offer(card)).ToHaveCountAsync(0);
    }
}
