using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;

// A reorder made against a revision a teammate has since moved on. Its own class: it reorders.
[TestFixture]
public class StaleReorderTests : SeededAcceptanceTest
{
    [Test]
    public async Task Backlog_WhenStaleSave_ShouldOfferUseMineOrKeepTheirs()
    {
        var backlog = new StoriesBacklog(Page);
        var list = new BacklogInteractions(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var before = (await list.KeysAsync()).Take(3).ToList();

        // The page keeps the backlog it has: every later read answers with the copy taken now, so the teammate's save never reaches it.
        var read = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/backlog?mode=list&filter=&includeCompleted=false");
        var snapshot = await read.TextAsync();
        await Page.RouteAsync(
            url => url.Contains("/backlog?", StringComparison.Ordinal),
            route => route.FulfillAsync(new() { ContentType = "application/json", Body = snapshot }));
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        await BacklogWrites.SetPointsAsync(tom, before[0], 8);

        var stale = await list.DragAsync(from: 0, to: 2);

        stale.Status.ShouldBe(409);
        await Expect(list.ConflictBanner).ToContainTextAsync("Tom Okafor");
        await Expect(list.UseMine).ToBeVisibleAsync();
        await Expect(list.KeepTheirs).ToBeVisibleAsync();

        var mine = await Page.RunAndWaitForResponseAsync(
            () => list.UseMine.ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/reorder", StringComparison.Ordinal));

        mine.Ok.ShouldBeTrue(await mine.TextAsync());
        await Expect(list.ConflictBanner).ToHaveCountAsync(0);

        await Page.UnrouteAllAsync(new() { Behavior = UnrouteBehavior.IgnoreErrors });
        await Page.ReloadAsync();
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        (await list.KeysAsync()).Take(3).ToArray().ShouldBe(new[] { before[1], before[2], before[0] });
    }
}
