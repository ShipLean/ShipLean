using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// A teammate changes a selected row while the move goes out: the others move and that row is reported. Each test uses rows of its own.
[TestFixture]
public class BulkConflictTests : SeededAcceptanceTest
{
    private static readonly string Sprint26 = SeedIds.Sprints.Sprint26.ToString();

    [Test]
    public async Task Bulk_WhenTeammateChangedOneRow_ShouldMoveTheOthers()
    {
        var (bar, frozen, tom) = await OpenAsync();
        var api = Page.Context.APIRequest;
        await bar.SelectAsync("SHIP-156", "SHIP-158", "SHIP-160");
        await BacklogWrites.SetPointsAsync(tom, "SHIP-158", 13);
        frozen.Freeze();

        var response = await bar.MoveToSprintAsync("Sprint 26");

        response.Status.ShouldBe(200);
        var result = JsonDocument.Parse(await response.TextAsync()).RootElement;
        result.GetProperty("written").EnumerateArray().Select(w => w.GetProperty("key").GetString()).ShouldBe(new[] { "SHIP-156", "SHIP-160" });
        result.GetProperty("stale").GetArrayLength().ShouldBe(1);
        await Expect(bar.Changed).ToBeVisibleAsync();
        await Expect(bar.ChangedRows).ToHaveCountAsync(1);
        await Expect(bar.ChangedRows.First).ToContainTextAsync("SHIP-158");
        await Expect(bar.ChangedRows.First).ToContainTextAsync("Tom Okafor");
        (await BulkReadBack.ReadAsync(api, "SHIP-156")).Sprint.ShouldBe("Sprint 26");
        (await BulkReadBack.ReadAsync(api, "SHIP-160")).Sprint.ShouldBe("Sprint 26");
        var left = await BulkReadBack.ReadAsync(api, "SHIP-158");
        left.Sprint.ShouldBeNull();
        left.Points.ShouldBe(13m);

        await bar.Leave.ClickAsync();
        await Expect(bar.Changed).ToHaveCountAsync(0);
        (await BulkReadBack.ReadAsync(api, "SHIP-158")).Sprint.ShouldBeNull();
    }

    [Test]
    public async Task Bulk_WhenIncludedAnyway_ShouldMoveThatRow()
    {
        var (bar, frozen, tom) = await OpenAsync();
        var api = Page.Context.APIRequest;
        await bar.SelectAsync("SHIP-162", "SHIP-163", "SHIP-165");
        await BacklogWrites.SetPointsAsync(tom, "SHIP-163", 8);
        frozen.Freeze();
        (await bar.MoveToSprintAsync("Sprint 26")).Status.ShouldBe(200);
        await Expect(bar.ChangedRows).ToHaveCountAsync(1);
        await frozen.ThawAsync(Page);

        var included = await bar.RunBulkAsync(() => bar.IncludeAnyway.ClickAsync());

        included.Status.ShouldBe(200);
        var result = JsonDocument.Parse(await included.TextAsync()).RootElement;
        result.GetProperty("written").EnumerateArray().Select(w => w.GetProperty("key").GetString()).ShouldBe(new[] { "SHIP-163" });
        result.GetProperty("stale").GetArrayLength().ShouldBe(0);
        await Expect(bar.Changed).ToHaveCountAsync(0);
        await Expect(bar.Row("SHIP-163")).ToHaveAttributeAsync("data-section", Sprint26);
        var held = await BulkReadBack.ReadAsync(api, "SHIP-163");
        held.Sprint.ShouldBe("Sprint 26");
        // The teammate's points were kept: the row was sent again against the revision they left.
        held.Points.ShouldBe(8m);

        // One Undo puts all three back, the included row too.
        var undone = await bar.UndoAsync();

        undone.Status.ShouldBe(200);
        JsonDocument.Parse(await undone.TextAsync()).RootElement.GetProperty("written").GetArrayLength().ShouldBe(3);
        foreach (var key in new[] { "SHIP-162", "SHIP-163", "SHIP-165" })
        {
            (await BulkReadBack.ReadAsync(api, key)).Sprint.ShouldBeNull();
        }

        (await BulkReadBack.ReadAsync(api, "SHIP-163")).Points.ShouldBe(8m);
    }

    [Test]
    public async Task Bulk_WhenRowChangedSinceMove_ShouldOfferOnlyLeaveItOnUndo()
    {
        var (bar, _, tom) = await OpenAsync();
        var api = Page.Context.APIRequest;
        await bar.SelectAsync("SHIP-166", "SHIP-170");
        (await bar.MoveToSprintAsync("Sprint 26")).Status.ShouldBe(200);
        await Expect(bar.Row("SHIP-170")).ToHaveAttributeAsync("data-section", Sprint26);
        await BacklogWrites.SetPointsAsync(tom, "SHIP-170", 5);

        var undone = await bar.UndoAsync();

        undone.Status.ShouldBe(200);
        var result = JsonDocument.Parse(await undone.TextAsync()).RootElement;
        result.GetProperty("written").EnumerateArray().Select(w => w.GetProperty("key").GetString()).ShouldBe(new[] { "SHIP-166" });
        result.GetProperty("stale").GetArrayLength().ShouldBe(1);
        await Expect(bar.ChangedRows).ToHaveCountAsync(1);
        await Expect(bar.ChangedRows.First).ToContainTextAsync("SHIP-170");
        await Expect(bar.Leave).ToBeVisibleAsync();
        await Expect(bar.IncludeAnyway).ToHaveCountAsync(0);
        (await BulkReadBack.ReadAsync(api, "SHIP-166")).Sprint.ShouldBeNull();
        (await BulkReadBack.ReadAsync(api, "SHIP-170")).Sprint.ShouldBe("Sprint 26");
    }

    private async Task<(BulkBar Bar, FrozenBacklog Frozen, IAPIRequestContext Tom)> OpenAsync()
    {
        var frozen = await FrozenBacklog.StartAsync(Page);
        await new PlanningBacklog(Page).OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        return (new BulkBar(Page), frozen, tom);
    }
}
