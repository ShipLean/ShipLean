using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// Three rows ticked through their own checkboxes in the Backlog, dragged as a block onto a row of Sprint 25. Its own class: it moves the items.
[TestFixture]
public class GroupDragMoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Drag_WhenSelectionDropped_ShouldMoveTheBlock()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        await moves.ShowWholePageAsync();
        await planning.OpenAsync("SHIP");
        foreach (var key in new[] { "SHIP-158", "SHIP-160", "SHIP-162" })
        {
            await moves.SelectAsync(key);
        }

        await Expect(moves.BulkBar.GetByTestId(BacklogTestIds.BulkCount)).ToContainTextAsync("3");

        await moves.PickUpAndHoverAsync("SHIP-158", moves.Row("SHIP-153"));
        await Expect(moves.DragBadge).ToHaveTextAsync("3 items");
        var response = await moves.DropAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var sent = PullKeys.ItemsOf(response.Request.PostData);
        sent.Count.ShouldBe(3);
        sent.ShouldAllBe(item => item.GetProperty("placement").GetProperty("slot").GetString() == "before");
        var result = System.Text.Json.JsonDocument.Parse(await response.TextAsync()).RootElement;
        result.GetProperty("written").GetArrayLength().ShouldBe(3);
        result.GetProperty("stale").GetArrayLength().ShouldBe(0);

        string[] expected = ["SHIP-151", "SHIP-158", "SHIP-160", "SHIP-162", "SHIP-153", "SHIP-147"];
        await Expect(moves.Row("SHIP-162")).ToHaveAttributeAsync("data-section", PullKeys.Sprint25);
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe(expected);

        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(moves.Row("SHIP-147")).ToBeVisibleAsync();
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe(expected);
        (await moves.KeysInAsync("backlog")).Take(2).ShouldBe(new[] { "SHIP-156", "SHIP-163" });
    }
}
