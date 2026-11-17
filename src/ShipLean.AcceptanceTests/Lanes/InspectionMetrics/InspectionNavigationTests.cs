using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.InspectionMetrics;
using ShipLean.AcceptanceTests.Pages.Shell;

namespace ShipLean.AcceptanceTests.Lanes.InspectionMetrics;

// Getting to Inspection on the seed: the side menu's Inspection section after Retrospectives opens the Overview, and its
// Lead time item opens the Lead time view. Only the page's roots are asserted; the views have their own tests.
[TestFixture]
public class InspectionNavigationTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    [Test]
    public async Task SideMenu_WhenInspectionClicked_ShouldOpenOverview()
    {
        var shell = new ShellFrame(Page);
        var inspection = new InspectionMetricsPage(Page);
        await shell.OpenAsync(ShellFrame.BacklogPath(Project));

        var retros = await shell.NavRetrospectives.BoundingBoxAsync();
        var item = await inspection.MenuItem.BoundingBoxAsync();
        retros!.Y.ShouldBeLessThan(item!.Y);

        await inspection.MenuItem.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"{InspectionMetricsPage.Path(Project)}$"));
        await Expect(inspection.Overview).ToBeAttachedAsync();
        await Expect(inspection.HeaderTitle).ToHaveTextAsync("Inspection");
        await Expect(inspection.Caveat).ToBeVisibleAsync();
        await Expect(inspection.MenuOverview).ToHaveAttributeAsync("aria-current", "page");
        await Expect(inspection.LeadTime).ToHaveCountAsync(0);
    }

    [Test]
    public async Task LeadTimeMenuItem_WhenClicked_ShouldShowLeadTimeSlot()
    {
        var inspection = new InspectionMetricsPage(Page);
        await new ShellFrame(Page).OpenAsync(InspectionMetricsPage.Path(Project));
        await Expect(inspection.Overview).ToBeAttachedAsync();

        await inspection.MenuLeadTime.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/inspection\?view=leadtime$"));
        await Expect(inspection.LeadTime).ToBeAttachedAsync();
        await Expect(inspection.Overview).ToHaveCountAsync(0);
        await Expect(inspection.MenuLeadTime).ToHaveAttributeAsync("aria-current", "page");
        await Expect(inspection.ViewTab("Lead time")).ToHaveAttributeAsync("aria-current", "page");
    }
}
