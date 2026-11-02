using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningAids;

// The strip, an open queue and the action bar at the two desktop widths the app is used at, on the Stories list and
// Planning. Component tests run at component size, so only here does the page's own layout show an overflow.
[TestFixture]
public class StripLayoutTests : SeededAcceptanceTest
{
    [TestCase(1280, "list")]
    [TestCase(1440, "list")]
    [TestCase(1280, "planning")]
    [TestCase(1440, "planning")]
    public async Task Strip_WhenQueueOpenAtDesktopWidth_ShouldNotOverflow(int width, string mode)
    {
        await Page.SetViewportSizeAsync(width, 900);
        var strip = new BeforeYouPlanStrip(Page);
        var keys = new BacklogKeys(Page);
        if (mode == "planning")
        {
            await new PlanningBacklog(Page).OpenAsync("SHIP");
        }
        else
        {
            await new StoriesBacklog(Page).OpenAsync("SHIP");
        }

        await Expect(strip.Strip).ToBeVisibleAsync();
        await AssertFitsAsync(width, "strip closed");

        await strip.Check(BeforeYouPlanStrip.NoCriteria).ClickAsync();
        await Expect(strip.QueueBar).ToBeVisibleAsync();
        await keys.TabToListAsync();
        await keys.PressAsync("x");
        await keys.PressAsync("j");
        await keys.PressAsync("x");
        await Expect(Page.GetByTestId(BacklogTestIds.BulkBar)).ToBeVisibleAsync();
        await AssertFitsAsync(width, "queue open with the bar");
    }

    // No horizontal scroll on the page, and the strip, the queue bar and the action bar end inside the viewport.
    private async Task AssertFitsAsync(int width, string when)
    {
        var overflow = await Page.EvaluateAsync<double[]>(
            @"ids => {
                const edge = id => { const e = document.querySelector(`[data-testid=""${id}""]`); return e ? e.getBoundingClientRect().right : 0; };
                return [document.documentElement.scrollWidth, ...ids.map(edge)];
            }",
            new[] { BacklogTestIds.PlanStrip, BacklogTestIds.PlanQueueBar, BacklogTestIds.BulkBar });
        overflow[0].ShouldBeLessThanOrEqualTo(width, $"page scrolls sideways at {width} ({when})");
        foreach (var right in overflow.Skip(1))
        {
            right.ShouldBeLessThanOrEqualTo(width, $"an element ends at {right} beyond {width} ({when})");
        }

        var stripScroll = await new BeforeYouPlanStrip(Page).Strip.EvaluateAsync<bool>("e => e.scrollWidth > e.clientWidth");
        stripScroll.ShouldBeFalse($"the strip clips its content at {width} ({when})");
    }
}
