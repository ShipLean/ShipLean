using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;
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

        // A list and a queue each far taller than the window: the checked rows are all unwritten, so they are all in the queue.
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        var added = await TeammateBulk.AddStoriesAsync(tom, SeedIds.Projects.Ship, 60, "Tall list");
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

        // The queue is taller than the window, and the bars stay in view at the top, the middle and the bottom of it.
        var rows = new StoriesBacklog(Page).Rows;
        (await rows.CountAsync()).ShouldBeGreaterThanOrEqualTo(added.Count);
        (await Page.EvaluateAsync<double>("() => document.documentElement.scrollHeight + [...document.querySelectorAll('*')].reduce((m, e) => Math.max(m, e.scrollHeight), 0)"))
            .ShouldBeGreaterThan(900 * 2, "the arranged list is not taller than the window");
        await rows.Nth(added.Count / 2).ScrollIntoViewIfNeededAsync();
        await AssertInViewAsync(width, "scrolled to the middle of the queue");
        await rows.Last.ScrollIntoViewIfNeededAsync();
        await AssertInViewAsync(width, "scrolled to the end of the queue");
        await rows.First.ScrollIntoViewIfNeededAsync();
        await AssertInViewAsync(width, "scrolled back to the top");
    }

    // The queue bar and the action bar are inside the window, top and bottom, wherever the list is scrolled to.
    private async Task AssertInViewAsync(int width, string when)
    {
        var edges = await Page.EvaluateAsync<double[]>(
            @"ids => ids.flatMap(id => { const e = document.querySelector(`[data-testid=""${id}""]`); const r = e ? e.getBoundingClientRect() : null; return r ? [r.top, r.bottom - window.innerHeight] : [Number.NaN, Number.NaN]; })",
            new[] { BacklogTestIds.PlanQueueBar, BacklogTestIds.BulkBar });
        var names = new[] { "queue bar", "action bar" };
        for (var i = 0; i < names.Length; i++)
        {
            double.IsNaN(edges[i * 2]).ShouldBeFalse($"the {names[i]} is not on the page at {width} ({when})");
            edges[i * 2].ShouldBeGreaterThanOrEqualTo(-0.5, $"the {names[i]} starts above the window at {width} ({when})");
            edges[(i * 2) + 1].ShouldBeLessThanOrEqualTo(0.5, $"the {names[i]} ends {edges[(i * 2) + 1]}px below the window at {width} ({when})");
        }
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
