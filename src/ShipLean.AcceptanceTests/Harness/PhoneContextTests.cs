using ShipLean.TestSupport;
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
        AcceptanceViewport.Phone.ShouldBe(new AcceptanceViewport(390, 844, HasTouch: true, IsMobile: true, DeviceScaleFactor: 1));
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

    // The shell's main region has overflow-auto, so on the taskboard (a desktop grid today) it scrolls sideways while
    // the document does not.
    [Test]
    public async Task TouchRules_WhenScopedToARegionThatScrollsSideways_ShouldReportItsOwnScroll()
    {
        await OpenTaskboardAsync();

        var region = await TouchRules.ViolationsAsync(Page, Page.GetByTestId(AppTestIds.ShellMain));

        region.ShouldContain(line => line.StartsWith(ShellMainDescription + " scrolls sideways", StringComparison.Ordinal));
        region.ShouldNotContain(line => line.StartsWith("page scrolls sideways", StringComparison.Ordinal));
    }

    [Test]
    public async Task TouchRules_WhenTheShellMainScrollsSideways_ShouldReportThePageScrolling()
    {
        await OpenTaskboardAsync();

        var page = await TouchRules.ViolationsAsync(Page);

        page.ShouldContain(line => line.StartsWith("page scrolls sideways in " + ShellMainDescription, StringComparison.Ordinal));
    }

    private const string ShellMainDescription = "main [data-testid=" + AppTestIds.ShellMain + "]";

    private async Task OpenTaskboardAsync()
    {
        await Page.GotoAsync(Links.Taskboard("SHIP", SeedIds.Sprints.Sprint24));
        await Expect(Page.GetByTestId(TaskboardTestIds.Card).First).ToBeVisibleAsync();
    }
}
