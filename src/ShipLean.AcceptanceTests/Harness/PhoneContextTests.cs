using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Harness;

// The phone context (K8) a class opts into, and TouchRules' region form (K9).
[TestFixture]
public class PhoneContextTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task PhoneContext_WhenOptedIn_ShouldBe390By844WithTouch()
    {
        await Page.GotoAsync(Links.Home());

        var context = await Page.EvaluateAsync<int[]>(
            "() => [window.innerWidth, window.innerHeight, navigator.maxTouchPoints, window.devicePixelRatio]");

        context[0].ShouldBe(390);
        context[1].ShouldBe(844);
        context[2].ShouldBeGreaterThan(0);
        context[3].ShouldBe(1);
    }

    [Test]
    public async Task TouchRules_WhenScopedToARegion_ShouldIgnoreViolationsOutsideIt()
    {
        await Page.GotoAsync(Links.WorkItem("SHIP", "SHIP-101"));
        var key = Page.GetByTestId(WorkItemTestIds.Key);
        await Expect(key).ToBeVisibleAsync();

        var page = await TouchRules.ViolationsAsync(Page);
        var region = await TouchRules.ViolationsAsync(Page, key);

        page.ShouldNotBeEmpty("Today's work item page has desktop-sized controls, so the whole page should break the rules.");
        region.ShouldBeEmpty();
    }
}
