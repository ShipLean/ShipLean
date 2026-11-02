using System.Diagnostics;
using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// The limit: a hundred rows moved in one action, through the UI. The timings go to the test output.
[TestFixture]
public class BulkHundredRowsTests : SeededAcceptanceTest
{
    [Test]
    public async Task Bulk_WhenHundredRowsMoved_ShouldShowInTheSprint()
    {
        var planning = new PlanningBacklog(Page);
        var bar = new BulkBar(Page);
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        var keys = await TeammateBulk.AddStoriesAsync(tom, SeedIds.Projects.Ship, 100, "Hundred");
        await planning.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var sprint26 = SeedIds.Sprints.Sprint26.ToString();
        await Expect(bar.Row(keys[^1])).ToBeVisibleAsync();
        await Expect(bar.Row(keys[0])).ToBeVisibleAsync();
        var inOrder = await BulkReadBack.InOrderAsync(Page.Context.APIRequest, keys.ToArray());
        await bar.SelectRangeAsync(inOrder[0], inOrder[^1], 100);

        var clock = Stopwatch.StartNew();
        var response = await bar.MoveToSprintAsync("Sprint 26");
        var answered = clock.ElapsedMilliseconds;
        await bar.WaitForSectionCountAsync(sprint26, 100);
        var shown = clock.ElapsedMilliseconds;

        response.Status.ShouldBe(200);
        var result = JsonDocument.Parse(await response.TextAsync()).RootElement;
        result.GetProperty("written").GetArrayLength().ShouldBe(100);
        result.GetProperty("stale").GetArrayLength().ShouldBe(0);
        await bar.WaitForSectionAsync(sprint26, inOrder);
        await TestContext.Out.WriteLineAsync($"100 rows: click to response {answered} ms, click to rows showing {shown} ms");
    }
}
