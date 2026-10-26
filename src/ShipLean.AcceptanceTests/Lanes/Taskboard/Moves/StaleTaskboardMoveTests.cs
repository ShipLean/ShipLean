using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Moves;

// A drop made against a revision a teammate has since moved on. Its own class: it moves a task Tom also moves.
[TestFixture]
public class StaleTaskboardMoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Taskboard_WhenMoveStale_ShouldSnapBack()
    {
        var board = new SprintTaskboard(Page);
        var moves = new TaskboardMoves(Page);
        await board.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        var row = board.RowTitled("Sign in with passkey");
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.ActiveColumn).GetByTestId(TaskboardTestIds.CardKey)).ToContainTextAsync(["SHIP-178"]);

        // The page keeps the Taskboard it has: every later read answers with the copy taken now, so Tom's move never reaches it.
        var read = await Page.Context.APIRequest.GetAsync($"/api/sprints/{SeedIds.Sprints.Sprint24}/taskboard");
        var snapshot = await read.TextAsync();
        await Page.RouteAsync(
            url => new Uri(url).AbsolutePath.EndsWith("/taskboard", StringComparison.Ordinal),
            route => route.FulfillAsync(new() { ContentType = "application/json", Body = snapshot }));
        await using var tom = await TaskboardTeammate.SignInAsTomAsync(Page, BaseUrl);
        await tom.MoveToStateAsync(SeedIds.Sprints.Sprint24, "SHIP-178", SeedIds.States.Ship.TaskInReview);

        var stale = await moves.DragToCellAsync(moves.Card("SHIP-178"), TaskboardMoves.Cell(row, TaskboardMoves.ClosedColumn));

        stale.Status.ShouldBe(409);
        await Expect(moves.Toasts).ToContainTextAsync("SHIP-178");
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.ActiveColumn).GetByTestId(TaskboardTestIds.CardKey)).ToContainTextAsync(["SHIP-178"]);
        await Expect(TaskboardMoves.Cell(row, TaskboardMoves.ClosedColumn).GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextString = "SHIP-178" })).ToHaveCountAsync(0);
    }
}
