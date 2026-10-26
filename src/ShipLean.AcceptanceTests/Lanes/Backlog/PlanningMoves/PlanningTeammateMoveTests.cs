using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningMoves;

// A teammate's move arriving over the hub while Planning is open. Its own class: the move changes the item.
[TestFixture]
public class PlanningTeammateMoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Planning_WhenTeammateMoves_ShouldAnimateAndToast()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        await planning.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var sprint25 = SeedIds.Sprints.Sprint25.ToString();
        var moved = (await moves.KeysInAsync("backlog"))[0];
        var target = (await moves.KeysInAsync(sprint25))[0];

        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        await TeammateSprintMove.MoveAsync(tom, moved, SeedIds.Sprints.Sprint25, target);

        // The row flashes and slides into Sprint 25, just above the item Tom put it before.
        await Expect(moves.Row(moved)).ToHaveAttributeAsync("data-flash", "true");
        await Expect(moves.Row(moved)).ToHaveAttributeAsync("data-section", sprint25);
        await Expect(moves.Toasts.Filter(new() { HasText = "Tom Okafor" }).First).ToContainTextAsync(moved);
        await moves.WaitForRowAboveAsync(moved, target);
    }
}
