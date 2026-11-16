using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Backlog.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Touch;

// Select mode and the bulk bar on the Stories List at a phone width (SHIP-716 D3), against the real K1. Only this task's own
// regions (the select bar, the bulk bar and its sheets) are checked for touch rules.
[TestFixture]
public class SelectModeTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Bulk_WhenTwoSelectedAndMovedToSprintOnPhone_ShouldMoveBoth()
    {
        var backlog = new StoriesBacklog(Page);
        var select = new SelectMode(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var first = (await backlog.RowKeys.Nth(5).InnerTextAsync()).Trim();
        var second = (await backlog.RowKeys.Nth(6).InnerTextAsync()).Trim();
        var api = Page.Context.APIRequest;
        (await BulkReadBack.ReadAsync(api, first)).Sprint.ShouldNotBe("Sprint 26");
        (await BulkReadBack.ReadAsync(api, second)).Sprint.ShouldNotBe("Sprint 26");

        await select.StartAsync();
        (await TouchRules.ViolationsAsync(Page, select.SelectBar)).ShouldBeEmpty();
        await select.Box(first).TapAsync();
        await select.Box(second).TapAsync();

        await Expect(select.BarCount).ToHaveTextAsync("2 selected");
        (await TouchRules.ViolationsAsync(Page, select.Bar)).ShouldBeEmpty();
        await select.SprintButton.TapAsync();
        await Expect(select.Sheet).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, select.Sheet)).ShouldBeEmpty();
        var response = await Page.RunAndWaitForResponseAsync(
            async () => await select.SprintOption("Sprint 26").TapAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

        response.Ok.ShouldBeTrue(await response.TextAsync());
        (await BulkReadBack.ReadAsync(api, first)).Sprint.ShouldBe("Sprint 26");
        (await BulkReadBack.ReadAsync(api, second)).Sprint.ShouldBe("Sprint 26");
    }

    [Test]
    public async Task SelectMode_WhenDoneTappedOnPhone_ShouldShowHandlesAgain()
    {
        var backlog = new StoriesBacklog(Page);
        var rows = new TouchRows(Page);
        var select = new SelectMode(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var key = (await backlog.RowKeys.First.InnerTextAsync()).Trim();
        await Expect(rows.Handle(key)).ToBeVisibleAsync();

        await select.StartAsync();
        await Expect(select.Boxes.First).ToBeVisibleAsync();
        await Expect(rows.Handle(key)).ToHaveCountAsync(0);
        await select.Box(key).TapAsync();
        await Expect(select.Bar).ToBeVisibleAsync();
        await select.DoneButton.TapAsync();

        await Expect(select.Boxes).ToHaveCountAsync(0);
        await Expect(select.SelectBar).ToHaveCountAsync(0);
        await Expect(select.Bar).ToHaveCountAsync(0);
        await Expect(rows.Handle(key)).ToBeVisibleAsync();
    }
}
