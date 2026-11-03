using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// The pull line dragged past what fits: the sum warns, and Pull still moves every row above it. Its own class: it moves the items.
[TestFixture]
public class PullDraggedOverTests : SeededAcceptanceTest
{
    [Test]
    public async Task Pull_WhenDraggedOver_ShouldWarnAndMove()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        var pull = new PullLine(Page);
        await Page.SetViewportSizeAsync(1280, 2400);
        await planning.OpenAsync("SHIP");
        await Expect(pull.Line).ToBeVisibleAsync();
        await Expect(pull.Reset).ToHaveCountAsync(0);

        await pull.DragGripBelowAsync(moves.Row("SHIP-160"));

        await Expect(pull.Sum).ToHaveAttributeAsync("data-over", "true");
        await Expect(pull.Sum).ToContainTextAsync("13 + 13 = 26 / 22");
        await Expect(pull.Sum).ToContainTextAsync("+4 over");
        await Expect(pull.Reset).ToBeVisibleAsync();
        await Expect(pull.Line).ToHaveAttributeAsync("data-over", "true");
        (await pull.KeysAboveAsync()).ShouldBe(new[] { "SHIP-156", "SHIP-158", "SHIP-160" });
        await Expect(pull.Go).ToBeEnabledAsync();

        var response = await Page.RunAndWaitForResponseAsync(
            () => pull.Go.ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

        response.Ok.ShouldBeTrue(await response.TextAsync());
        PullKeys.ItemsOf(response.Request.PostData).Count.ShouldBe(3);
        await Expect(moves.Row("SHIP-160")).ToHaveAttributeAsync("data-section", PullKeys.Sprint25);
        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(moves.Row("SHIP-160")).ToBeVisibleAsync();
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe(new[] { "SHIP-151", "SHIP-153", "SHIP-147", "SHIP-156", "SHIP-158", "SHIP-160" });
        await Expect(planning.Section("Sprint 25")).ToContainTextAsync("26 / 22 pts");
        await Expect(planning.Over("Sprint 25")).ToBeVisibleAsync();
    }
}
