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

    // The shell's main region has overflow-auto, so content wider than it scrolls the region sideways while the
    // document does not. The page puts the wide content in itself, so the test does not depend on any screen's layout.
    [Test]
    public async Task TouchRules_WhenScopedToARegionThatScrollsSideways_ShouldReportItsOwnScroll()
    {
        await OpenWithWideContentInShellMainAsync();

        var region = await TouchRules.ViolationsAsync(Page, Page.GetByTestId(AppTestIds.ShellMain));

        region.ShouldContain(line => line.StartsWith(ShellMainDescription + " scrolls sideways", StringComparison.Ordinal));
        region.ShouldNotContain(line => line.StartsWith("page scrolls sideways", StringComparison.Ordinal));
    }

    [Test]
    public async Task TouchRules_WhenTheShellMainScrollsSideways_ShouldReportThePageScrolling()
    {
        await OpenWithWideContentInShellMainAsync();

        var page = await TouchRules.ViolationsAsync(Page);

        page.ShouldContain(line => line.StartsWith("page scrolls sideways in " + ShellMainDescription, StringComparison.Ordinal));
    }

    // A mobile context widens window.innerWidth to fit overflowing content, so the page check must use the layout
    // viewport (documentElement.clientWidth), which stays at 390.
    [Test]
    public async Task TouchRules_WhenTheDocumentScrollsSideways_ShouldReportThePageScrolling()
    {
        await Page.GotoAsync(Links.Home());
        await Expect(Page.GetByTestId(AppTestIds.ShellMain)).ToBeVisibleAsync();
        await Page.EvaluateAsync("() => { const wide = document.createElement('div'); wide.style.width = '1200px'; wide.style.height = '1px'; document.body.append(wide); }");

        var page = await TouchRules.ViolationsAsync(Page);

        page.ShouldContain(line => line.StartsWith("page scrolls sideways: ", StringComparison.Ordinal));
    }

    private const string ShellMainDescription ="main [data-testid=" + AppTestIds.ShellMain + "]";

    private async Task OpenWithWideContentInShellMainAsync()
    {
        await Page.GotoAsync(Links.Home());
        var main = Page.GetByTestId(AppTestIds.ShellMain);
        await Expect(main).ToBeVisibleAsync();
        await main.EvaluateAsync("main => { const wide = document.createElement('div'); wide.style.width = '1200px'; wide.style.height = '1px'; main.append(wide); }");
    }
}
