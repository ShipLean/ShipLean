using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// Remove and Undo on the Stories list, which has no sections: the list mode half of what Planning proves in BulkMoveTests.
[TestFixture]
public class BulkRemoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Remove_WhenBulk_ShouldHideRowsAndUndoBringsThemBack()
    {
        var backlog = new StoriesBacklog(Page);
        var bar = new BulkBar(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var api = Page.Context.APIRequest;
        var keysBefore = await backlog.RowKeys.AllInnerTextsAsync();
        var picked = new[] { "SHIP-158", "SHIP-163" };
        var orderBefore = await BulkReadBack.InOrderAsync(api, keysBefore.ToArray());
        var statesBefore = new Dictionary<string, string>();
        foreach (var key in picked)
        {
            statesBefore[key] = (await BulkReadBack.ReadAsync(api, key)).State;
        }

        await bar.SelectAsync(picked);
        var removed = await bar.RunBulkAsync(() => bar.Remove.ClickAsync());

        removed.Status.ShouldBe(200);
        JsonDocument.Parse(await removed.TextAsync()).RootElement.GetProperty("written").GetArrayLength().ShouldBe(2);
        await Expect(bar.Row("SHIP-158")).ToHaveCountAsync(0);
        await Expect(bar.Row("SHIP-163")).ToHaveCountAsync(0);
        await Expect(bar.Bar).ToHaveCountAsync(0);
        foreach (var key in picked)
        {
            (await BulkReadBack.ReadAsync(api, key)).State.ShouldBe("Removed");
        }

        var undone = await bar.UndoAsync();

        undone.Status.ShouldBe(200);
        JsonDocument.Parse(await undone.TextAsync()).RootElement.GetProperty("stale").GetArrayLength().ShouldBe(0);
        await Expect(bar.Row("SHIP-158")).ToBeVisibleAsync();
        await Expect(bar.Row("SHIP-163")).ToBeVisibleAsync();
        foreach (var key in picked)
        {
            (await BulkReadBack.ReadAsync(api, key)).State.ShouldBe(statesBefore[key]);
        }

        await Expect(backlog.RowKeys).ToHaveTextAsync(keysBefore.ToArray());
        (await BulkReadBack.InOrderAsync(api, keysBefore.ToArray())).ShouldBe(orderBefore);
    }

    [Test]
    public async Task Bulk_WhenUndoneInTheList_ShouldRestoreSprintAndRank()
    {
        var backlog = new StoriesBacklog(Page);
        var bar = new BulkBar(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var api = Page.Context.APIRequest;
        var keysBefore = await backlog.RowKeys.AllInnerTextsAsync();
        var inSprint = await BulkReadBack.ReadAsync(api, "SHIP-153");
        inSprint.Sprint.ShouldBe("Sprint 25");

        // In the list, Top keeps the sprint and moves the row to the head.
        await bar.SelectAsync("SHIP-153", "SHIP-165");
        var moved = await bar.RunBulkAsync(() => bar.MoveTop.ClickAsync());
        moved.Status.ShouldBe(200);
        await Expect(backlog.RowKeys.First).ToHaveTextAsync("SHIP-153");
        (await BulkReadBack.ReadAsync(api, "SHIP-153")).Sprint.ShouldBe("Sprint 25");

        var undone = await bar.UndoAsync();

        undone.Status.ShouldBe(200);
        await Expect(backlog.RowKeys).ToHaveTextAsync(keysBefore.ToArray());
        (await BulkReadBack.ReadAsync(api, "SHIP-153")).Sprint.ShouldBe("Sprint 25");
        (await BulkReadBack.InOrderAsync(api, keysBefore.ToArray())).ShouldBe(keysBefore.ToList());
    }
}
