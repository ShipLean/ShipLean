using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningMoves;

// A row dragged from the Backlog onto a row of Sprint 25 on the seeded Storefront project. Its own class: it moves the item.
[TestFixture]
public class PlanningDragTests : SeededAcceptanceTest
{
    [Test]
    public async Task Planning_WhenRowDraggedToSprint_ShouldSetSprintAndOrder()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        await moves.ShowWholePageAsync();
        await planning.OpenAsync("SHIP");
        var backlogId = "backlog";
        var sprint25 = SeedIds.Sprints.Sprint25.ToString();
        var moved = (await moves.KeysInAsync(backlogId))[0];
        // The last row of Sprint 25 is the nearest row above the Backlog's first.
        var inSprintBefore = await moves.KeysInAsync(sprint25);
        var target = inSprintBefore[^1];

        // The reorder is the second write of the drop: its answer means both were saved.
        var response = await moves.DragOntoRowAsync(moved, target);

        response.Ok.ShouldBeTrue(await response.TextAsync());
        await Expect(moves.Toasts.First).ToContainTextAsync($"{moved} → Sprint 25");
        (await moves.SectionOfAsync(moved)).ShouldBe(sprint25);

        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(moves.Row(moved)).ToBeVisibleAsync();
        var inSprint = await moves.KeysInAsync(sprint25);
        inSprint[inSprint.IndexOf(moved) + 1].ShouldBe(target);
        (await moves.KeysInAsync(backlogId)).ShouldNotContain(moved);
    }
}
