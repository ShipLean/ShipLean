using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// Puts the Planning page in the state with the action bar, the changed-rows report and the Undo toast all showing.
internal static class BulkLayout
{
    // How far past the viewport's sides and bottom the bar and the report reach, and how far the page is wider than the window.
    private const string Reach = """
        ([barId, changedId]) => {
          const past = (id, side) => Math.max(0, ...[...document.querySelectorAll(`[data-testid="${id}"], [data-testid="${id}"] *`)]
            .map(e => side === 'bottom' ? e.getBoundingClientRect().bottom - window.innerHeight : Math.max(e.getBoundingClientRect().right - window.innerWidth, -e.getBoundingClientRect().left)));
          return { page: document.documentElement.scrollWidth - window.innerWidth, barSide: past(barId, 'side'), changedSide: past(changedId, 'side'), barBottom: past(barId, 'bottom'), changedBottom: past(changedId, 'bottom') };
        }
        """;

    public static async Task<JsonElement> ShowAsync(IPage page, string baseUrl, int width, string first, string changed, string last)
    {
        await page.SetViewportSizeAsync(width, 900);
        var frozen = await FrozenBacklog.StartAsync(page);
        await new PlanningBacklog(page).OpenAsync("SHIP");
        await new LiveStatus(page).WaitUntilLiveAsync();
        var bar = new BulkBar(page);
        var tom = await BacklogWrites.SignInAsTomAsync(page, baseUrl);
        await bar.SelectAsync(first, changed, last);
        frozen.Freeze();
        await BacklogWrites.SetPointsAsync(tom, changed, 13);
        (await bar.MoveToSprintAsync("Sprint 26")).Status.ShouldBe(200);
        await Assertions.Expect(bar.Changed).ToBeVisibleAsync();
        await Assertions.Expect(bar.Undo).ToBeVisibleAsync();
        await Assertions.Expect(bar.Bar).ToBeVisibleAsync();
        return await page.EvaluateAsync<JsonElement>(Reach, new object[] { BacklogTestIds.BulkBar, BacklogTestIds.BulkChanged });
    }
}
