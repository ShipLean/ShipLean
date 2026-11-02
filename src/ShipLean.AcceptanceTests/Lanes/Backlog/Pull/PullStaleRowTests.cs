using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// Pull with one of the rows above the line changed by a teammate since the page read it. Its own class: it moves the items.
[TestFixture]
public class PullStaleRowTests : SeededAcceptanceTest
{
    [Test]
    public async Task Pull_WhenOneRowStale_ShouldMoveTheOthersAndReportIt()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        var pull = new PullLine(Page);
        var frozen = new FrozenBacklog(Page);
        await Page.SetViewportSizeAsync(1280, 2400);
        await frozen.StartAsync();
        await planning.OpenAsync("SHIP");
        await Expect(pull.Line).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await pull.DragGripBelowAsync(moves.Row("SHIP-160"));
        await Expect(pull.Sum).ToContainTextAsync("= 26 / 22");

        frozen.Freeze();
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        await BacklogWrites.SetPointsAsync(tom, "SHIP-158", 13);

        var response = await Page.RunAndWaitForResponseAsync(
            () => pull.Go.ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var result = JsonDocument.Parse(await response.TextAsync()).RootElement;
        result.GetProperty("written").GetArrayLength().ShouldBe(2);
        result.GetProperty("stale").GetArrayLength().ShouldBe(1);
        await Expect(moves.Row("SHIP-156")).ToHaveAttributeAsync("data-section", PullKeys.Sprint25);
        await Expect(moves.Row("SHIP-160")).ToHaveAttributeAsync("data-section", PullKeys.Sprint25);
        var changed = Page.GetByTestId(BacklogTestIds.BulkChanged);
        await Expect(changed).ToBeVisibleAsync();
        await Expect(changed.GetByTestId(BacklogTestIds.BulkChangedRow)).ToHaveCountAsync(1);
        await Expect(changed).ToContainTextAsync("SHIP-158");
        await Expect(changed).ToContainTextAsync("Tom Okafor");

        var included = await Page.RunAndWaitForResponseAsync(
            () => changed.GetByTestId(BacklogTestIds.BulkIncludeAnyway).ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

        included.Ok.ShouldBeTrue(await included.TextAsync());
        JsonDocument.Parse(await included.TextAsync()).RootElement.GetProperty("stale").GetArrayLength().ShouldBe(0);
        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(moves.Row("SHIP-158")).ToBeVisibleAsync();
        (await moves.KeysInAsync(PullKeys.Sprint25)).ShouldBe(new[] { "SHIP-151", "SHIP-153", "SHIP-147", "SHIP-156", "SHIP-158", "SHIP-160" });
        // Tom's points survive: Include it anyway moved the row and changed nothing else.
        await Expect(moves.Row("SHIP-158").GetByTestId(BacklogTestIds.Points)).ToHaveTextAsync("13");
    }
}
