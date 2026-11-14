using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Board.Touch;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;
using ShipLean.TestSupport.Usage;

namespace ShipLean.AcceptanceTests.Lanes.Board.Touch;

// Long-press drag of a card on a phone (SHIP-719 D2). The touch rules are checked on this task's regions only: each card
// while one is lifted. Each test moves or opens a card of its own.
[TestFixture]
public class BoardTouchDragTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private async Task<BoardScreen> OpenAsync()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await new BoardPager(Page).WaitAsync();
        return board;
    }

    [Test]
    public async Task Card_WhenLongPressedAndDraggedToEdgeOnPhone_ShouldTurnPageAndDrop()
    {
        var board = await OpenAsync();
        var drag = new BoardTouchDrag(Page);
        var card = board.CardTitled("Export sprint report to CSV");
        await drag.ShowColumnAsync("Ready");
        Recorder.Clear();

        await drag.LiftAsync(card);
        (await TouchRules.ViolationsAsync(Page, CardActionsSheet.More(card))).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, drag.Ghost)).ShouldBeEmpty();
        var cell = board.CellOfTitled("Export sprint report to CSV");
        var moved = await drag.DragLiftedToColumnAsync(card, cell, "In progress");

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(new BoardMoves(Page).Toasts.Filter(new LocatorFilterOptions { HasText = "Assigned to you" })).ToHaveCountAsync(1);
        await Expect(board.CellOfTitled("Export sprint report to CSV")).ToHaveAttributeAsync("data-column", "col-in-progress");
        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.WorkItemMoved).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.TouchDrag);
    }

    [Test]
    public async Task Card_WhenLongPressedWithoutMovingOnPhone_ShouldOpenActions()
    {
        var board = await OpenAsync();
        var drag = new BoardTouchDrag(Page);
        var sheets = new CardActionsSheet(Page);

        await drag.ShowColumnAsync("In progress");
        await drag.LiftAsync(board.CardTitled("Address autocomplete"));
        await drag.ReleaseAsync();

        await Expect(sheets.ActionsFor("SHIP-144")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(ItemPeekTestIds.Dialog)).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, sheets.ActionsFor("SHIP-144"))).ShouldBeEmpty();
    }
}
