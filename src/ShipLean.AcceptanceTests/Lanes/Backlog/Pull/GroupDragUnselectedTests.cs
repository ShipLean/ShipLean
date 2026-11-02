using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// With two rows ticked, a row that is not ticked is picked up by its grip: only it moves. Its own class: it moves the item.
[TestFixture]
public class GroupDragUnselectedTests : SeededAcceptanceTest
{
    [Test]
    public async Task Drag_WhenUnselectedRowDropped_ShouldMoveOnlyIt()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        await moves.ShowWholePageAsync();
        await planning.OpenAsync("SHIP");
        await moves.SelectAsync("SHIP-158");
        await moves.SelectAsync("SHIP-160");

        await moves.PickUpAndHoverAsync("SHIP-170", moves.Row("SHIP-153"));
        await Expect(moves.DragBadge).ToHaveCountAsync(0);
        // A single row is a single write: its sprint change, then its reorder, whose answer means both were saved.
        var response = await Page.RunAndWaitForResponseAsync(
            () => Page.Mouse.UpAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/reorder", StringComparison.Ordinal));

        response.Ok.ShouldBeTrue(await response.TextAsync());
        response.Url.ShouldContain("/api/work-items/");
        await Expect(moves.Row("SHIP-170")).ToHaveAttributeAsync("data-section", PullKeys.Sprint25);
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe(new[] { "SHIP-151", "SHIP-170", "SHIP-153", "SHIP-147" });
        (await moves.KeysInAsync("backlog")).ShouldContain("SHIP-158");
        (await moves.KeysInAsync("backlog")).ShouldContain("SHIP-160");

        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(moves.Row("SHIP-170")).ToBeVisibleAsync();
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe(new[] { "SHIP-151", "SHIP-170", "SHIP-153", "SHIP-147" });
    }
}
