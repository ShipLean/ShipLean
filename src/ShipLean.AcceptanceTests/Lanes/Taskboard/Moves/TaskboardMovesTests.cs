using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Moves;

// Sprint 24's Taskboard on the seeded Storefront project (SHIP), signed in as Joel, grouped by Stories (the default:
// the grouping tests are in another class with a database of its own). Each test moves tasks of its own.
[TestFixture]
public class TaskboardMovesTests : SeededAcceptanceTest
{
    private const string PaySavedCard = "Pay with saved card";
    private const string Passkey = "Sign in with passkey";
    private const string AddressAutocomplete = "Address autocomplete";

    private async Task<(SprintTaskboard Board, TaskboardMoves Moves)> OpenAsync()
    {
        var board = new SprintTaskboard(Page);
        await board.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        return (board, new TaskboardMoves(Page));
    }

    [Test]
    public async Task Taskboard_WhenCardDroppedInColumn_ShouldSetTaskState()
    {
        var (board, moves) = await OpenAsync();
        var row = board.RowTitled(PaySavedCard);
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.ActiveColumn).GetByTestId(TaskboardTestIds.CardKey)).ToContainTextAsync(["SHIP-172"]);

        var moved = await moves.DragToCellAsync(moves.Card("SHIP-172"), TaskboardMoves.Cell(row, TaskboardMoves.InReviewColumn));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.InReviewColumn).GetByTestId(TaskboardTestIds.CardKey)).ToContainTextAsync(["SHIP-172"]);
        await Page.ReloadAsync();
        await board.Grid.WaitForAsync();
        await Expect(TaskboardMoves.Cell(board.RowTitled(PaySavedCard), TaskboardMoves.InReviewColumn).GetByTestId(TaskboardTestIds.CardKey))
            .ToContainTextAsync(["SHIP-172"]);
        await Expect(TaskboardMoves.Cell(board.RowTitled(PaySavedCard), TaskboardMoves.ActiveColumn).GetByTestId(TaskboardTestIds.Card)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Taskboard_WhenPickedUp_ShouldAssignMeAndStartFirstInProgress()
    {
        var (board, moves) = await OpenAsync();
        var row = board.RowTitled(PaySavedCard);
        var before = moves.Card("SHIP-173").GetByTestId(TaskboardTestIds.CardAssignee).GetByTestId(ComponentTestIds.Avatar);
        await Expect(before).ToHaveAttributeAsync("data-variant", "none");
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.NewColumn).GetByTestId(TaskboardTestIds.CardKey)).ToContainTextAsync(["SHIP-173"]);

        var moved = await moves.PickUpAsync(moves.Card("SHIP-173"));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Page.ReloadAsync();
        await board.Grid.WaitForAsync();
        var card = moves.Card("SHIP-173");
        await Expect(TaskboardMoves.Cell(board.RowTitled(PaySavedCard), TaskboardMoves.ActiveColumn).GetByTestId(TaskboardTestIds.CardKey))
            .ToContainTextAsync(["SHIP-173"]);
        await Expect(card.GetByTestId(TaskboardTestIds.CardAssignee).GetByTestId(ComponentTestIds.Avatar)).ToHaveAttributeAsync("title", "Joel Dickson");
        await Expect(card.GetByTestId(TaskboardTestIds.CardPickUp)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Taskboard_WhenTaskDraggedToOtherRow_ShouldReparent()
    {
        var (board, moves) = await OpenAsync();
        await Expect(board.RowTitled(Passkey).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-179" })).ToHaveCountAsync(1);

        var moved = await moves.DragToCellAsync(
            moves.Card("SHIP-179"),
            TaskboardMoves.Cell(board.RowTitled(PaySavedCard), TaskboardMoves.NewColumn));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(board.RowTitled(PaySavedCard).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-179" })).ToHaveCountAsync(1);
        await Page.ReloadAsync();
        await board.Grid.WaitForAsync();
        await Expect(board.RowTitled(PaySavedCard).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-179" })).ToHaveCountAsync(1);
        await Expect(board.RowTitled(Passkey).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-179" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Taskboard_WhenLastTaskClosed_ShouldOfferMoveToResolved()
    {
        var (board, moves) = await OpenAsync();
        var row = board.RowTitled(AddressAutocomplete);
        await Expect(row.GetByTestId(TaskboardTestIds.RowOffer)).ToHaveCountAsync(0);

        var moved = await moves.DragToCellAsync(moves.Card("SHIP-176"), TaskboardMoves.Cell(row, TaskboardMoves.ClosedColumn));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(row.GetByTestId(TaskboardTestIds.RowOffer)).ToContainTextAsync("Move story to Resolved");

        var resolved = await moves.MoveToResolvedAsync(row);

        resolved.Ok.ShouldBeTrue(await resolved.TextAsync());
        await Expect(row.GetByTestId(TaskboardTestIds.RowOffer)).ToHaveCountAsync(0);
        await Page.ReloadAsync();
        await board.Grid.WaitForAsync();
        await Expect(board.RowTitled(AddressAutocomplete).GetByTestId(TaskboardTestIds.RowOffer)).ToHaveCountAsync(0);
        await Expect(board.RowTitled(AddressAutocomplete).GetByTestId(TaskboardTestIds.RowState)).ToHaveValueAsync(SeedIds.States.Ship.StoryResolved.ToString());
    }
}
