using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// Moving a selection on the Planning view against the real K1. Each test uses rows of its own; Sprint 26 starts empty.
[TestFixture]
public class BulkMoveTests : SeededAcceptanceTest
{
    private static readonly string Sprint26 = SeedIds.Sprints.Sprint26.ToString();

    [Test]
    public async Task Bulk_WhenRowsMovedToSprint_ShouldShowInTheSprint()
    {
        var planning = new PlanningBacklog(Page);
        var bar = new BulkBar(Page);
        await planning.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var api = Page.Context.APIRequest;
        var orderBefore = await BulkReadBack.InOrderAsync(api, "SHIP-156", "SHIP-158");

        await bar.SelectAsync("SHIP-156", "SHIP-158");
        await Expect(bar.SprintButton("Sprint 26").GetByTestId(BacklogTestIds.BulkSum)).ToHaveTextAsync("0 + 11 = 11 / 21");
        var response = await bar.MoveToSprintAsync("Sprint 26");

        response.Status.ShouldBe(200);
        var result = JsonDocument.Parse(await response.TextAsync()).RootElement;
        result.GetProperty("written").GetArrayLength().ShouldBe(2);
        result.GetProperty("stale").GetArrayLength().ShouldBe(0);
        await Expect(bar.Row("SHIP-156")).ToHaveAttributeAsync("data-section", Sprint26);
        await Expect(bar.Row("SHIP-158")).ToHaveAttributeAsync("data-section", Sprint26);
        (await BulkReadBack.ReadAsync(api, "SHIP-156")).Sprint.ShouldBe("Sprint 26");
        (await BulkReadBack.ReadAsync(api, "SHIP-158")).Sprint.ShouldBe("Sprint 26");
        // A move to a sprint keeps each row's rank.
        (await BulkReadBack.InOrderAsync(api, "SHIP-156", "SHIP-158")).ShouldBe(orderBefore);
        await bar.WaitForSectionAsync(Sprint26, orderBefore);
    }

    [Test]
    public async Task Bulk_WhenMovedToTop_ShouldKeepTheirOrder()
    {
        var planning = new PlanningBacklog(Page);
        var bar = new BulkBar(Page);
        await planning.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var api = Page.Context.APIRequest;
        var before = (await bar.RowsAsync()).Where(r => r.Section == "backlog").Select(r => r.Key).ToList();
        var picked = new[] { before[4], before[6], before[2] };
        var inRank = before.Where(picked.Contains).ToList();

        // Ticked out of order: the selection lands in rank order, not click order.
        await bar.SelectAsync(picked);
        var response = await bar.RunBulkAsync(() => bar.MoveTop.ClickAsync());

        response.Status.ShouldBe(200);
        JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("stale").GetArrayLength().ShouldBe(0);
        var expected = inRank.Concat(before.Where(k => !picked.Contains(k))).ToList();
        await bar.WaitForSectionAsync("backlog", expected);
        (await BulkReadBack.InOrderAsync(api, expected.ToArray())).ShouldBe(expected);
    }

    [Test]
    public async Task Bulk_WhenUndone_ShouldRestoreSprintAndRank()
    {
        var planning = new PlanningBacklog(Page);
        var bar = new BulkBar(Page);
        await planning.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var api = Page.Context.APIRequest;
        var sprintOrder = await BulkReadBack.InOrderAsync(api, "SHIP-151", "SHIP-153", "SHIP-147");
        var backlogBefore = (await bar.RowsAsync()).Where(r => r.Section == "backlog").Select(r => r.Key).ToList();
        var middle = sprintOrder[1];

        // A sprint row sent to Top goes to the Backlog, first.
        await bar.SelectAsync(middle);
        var moved = await bar.RunBulkAsync(() => bar.MoveTop.ClickAsync());
        moved.Status.ShouldBe(200);
        await Expect(bar.Row(middle)).ToHaveAttributeAsync("data-section", "backlog");
        var afterMove = await BulkReadBack.ReadAsync(api, middle);
        afterMove.Sprint.ShouldBeNull();
        await bar.WaitForSectionAsync("backlog", new[] { middle }.Concat(backlogBefore));

        var undone = await bar.UndoAsync();

        undone.Status.ShouldBe(200);
        JsonDocument.Parse(await undone.TextAsync()).RootElement.GetProperty("stale").GetArrayLength().ShouldBe(0);
        await Expect(bar.Row(middle)).ToHaveAttributeAsync("data-section", SeedIds.Sprints.Sprint25.ToString());
        (await BulkReadBack.ReadAsync(api, middle)).Sprint.ShouldBe("Sprint 25");
        (await BulkReadBack.InOrderAsync(api, "SHIP-151", "SHIP-153", "SHIP-147")).ShouldBe(sprintOrder);
        await bar.WaitForSectionAsync(SeedIds.Sprints.Sprint25.ToString(), sprintOrder);
        await bar.WaitForSectionAsync("backlog", backlogBefore);
    }
}
