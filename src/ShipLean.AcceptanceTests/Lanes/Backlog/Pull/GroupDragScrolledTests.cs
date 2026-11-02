using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// A group drag in a normal-sized window, with the page scrolled so the drop target sits close to the top edge of the viewport,
// where dnd-kit's autoscroll acts. Its own class: it moves the items.
[TestFixture]
public class GroupDragScrolledTests : SeededAcceptanceTest
{
    [Test]
    public async Task Drag_WhenPageScrolledAndTargetNearEdge_ShouldDropOnTheTarget()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        await Page.SetViewportSizeAsync(1280, 720);
        await planning.OpenAsync("SHIP");
        await moves.SelectAsync("SHIP-158");
        await moves.SelectAsync("SHIP-160");
        await PlanningDragging.ScrollRowToAsync(moves.Row("SHIP-153"), 120);
        await Expect(moves.Row("SHIP-160")).ToBeInViewportAsync();
        // The page really scrolled, and the target is inside the top fifth of the viewport, where autoscroll acts.
        var top = (await moves.Row("SHIP-153").BoundingBoxAsync())!.Y;
        top.ShouldBeInRange(100, 144);

        await moves.PickUpAndHoverAsync("SHIP-158", moves.Row("SHIP-153"));
        var response = await moves.DropAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        await Expect(moves.Row("SHIP-160")).ToHaveAttributeAsync("data-section", PullKeys.Sprint25);
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe(new[] { "SHIP-151", "SHIP-158", "SHIP-160", "SHIP-153", "SHIP-147" });
    }
}
