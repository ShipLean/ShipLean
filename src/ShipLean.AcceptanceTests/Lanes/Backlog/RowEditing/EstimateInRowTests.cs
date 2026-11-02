using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.RowEditing;

// Estimating in the row of the Stories list: Enter walks down and each save persists; a teammate's change offers Use mine.
[TestFixture]
public class EstimateInRowTests : SeededAcceptanceTest
{
    private static async Task<int?> PointsOnServerAsync(IAPIRequestContext request, string key)
    {
        var item = await BacklogWrites.GetItemAsync(request, key);
        var points = item.GetProperty("points");
        return points.ValueKind == JsonValueKind.Null ? null : points.GetInt32();
    }

    [Test]
    public async Task Estimate_WhenEnterWalksDown_ShouldSaveEachRow()
    {
        var backlog = new StoriesBacklog(Page);
        var editing = new BacklogRowEditing(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var keys = (await editing.KeysAsync()).Take(4).ToList();

        await editing.PointsCell(keys[0]).ClickAsync();
        var values = new[] { "34", "55", "89" };
        for (var i = 0; i < values.Length; i++)
        {
            await Expect(editing.Row(keys[i]).GetByTestId(ShipLean.TestSupport.TestIds.BacklogTestIds.PointsInput)).ToBeFocusedAsync();
            var saved = await editing.TypeAndEnterAsync(values[i]);
            saved.Ok.ShouldBeTrue(await saved.TextAsync());
        }

        // The last Enter walked on to the next row, whose field is open and empty of changes; Esc leaves it with nothing sent.
        await Expect(editing.Row(keys[3]).GetByTestId(ShipLean.TestSupport.TestIds.BacklogTestIds.PointsInput)).ToBeFocusedAsync();
        await editing.PointsField.PressAsync("Escape");
        await Expect(editing.PointsField).ToHaveCountAsync(0);
        for (var i = 0; i < values.Length; i++)
        {
            await Expect(editing.PointsCell(keys[i])).ToHaveTextAsync(values[i]);
        }

        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        for (var i = 0; i < values.Length; i++)
        {
            (await PointsOnServerAsync(tom, keys[i])).ShouldBe(int.Parse(values[i]));
        }
    }

    [Test]
    public async Task Estimate_WhenTeammateChangedRow_ShouldOfferUseMine()
    {
        var backlog = new StoriesBacklog(Page);
        var list = new BacklogInteractions(Page);
        var editing = new BacklogRowEditing(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var key = (await editing.KeysAsync())[0];

        // The page keeps the backlog it has, so the teammate's save never reaches it (as in StaleReorderTests).
        var read = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/backlog?mode=list&filter=&includeCompleted=false");
        var snapshot = await read.TextAsync();
        await Page.RouteAsync(
            url => url.Contains("/backlog?", StringComparison.Ordinal),
            route => route.FulfillAsync(new() { ContentType = "application/json", Body = snapshot }));
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        await BacklogWrites.SetPointsAsync(tom, key, 8);

        await editing.PointsCell(key).ClickAsync();
        var stale = await editing.TypeAndEnterAsync("21");

        stale.Status.ShouldBe(409);
        await Expect(list.ConflictBanner).ToContainTextAsync("Tom Okafor");
        await Expect(list.KeepTheirs).ToBeVisibleAsync();
        (await PointsOnServerAsync(tom, key)).ShouldBe(8);

        var mine = await Page.RunAndWaitForResponseAsync(
            () => list.UseMine.ClickAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));

        mine.Ok.ShouldBeTrue(await mine.TextAsync());
        await Expect(list.ConflictBanner).ToHaveCountAsync(0);
        (await PointsOnServerAsync(tom, key)).ShouldBe(21);
    }
}
