using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Touch;

// Project settings, Integrations and Demos, on a phone (SHIP-732 P7 and P8, L732c), as Joel (an admin). MOB has a seeded Jira
// connection and a finished import. The YouTube Disconnect confirmation is checked in the component test
// (SettingsDemos.phone.spec.tsx): connecting a channel here needs the whole OAuth journey of YouTubeJourneyTests.
[TestFixture]
public class SettingsIntegrationsDemosTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Integrations_WhenOpenedOnAPhone_ShouldWrapRowsWithoutSidewaysScroll()
    {
        await Page.GotoAsync(Links.ProjectSettings("MOB", "integrations"));
        var tab = Page.GetByTestId(ImportTestIds.IntegrationsTab);
        await Expect(Page.GetByTestId(ImportTestIds.JobRow)).ToHaveCountAsync(1);

        (await tab.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth")).ShouldBeTrue("the tab scrolls sideways");
        (await Page.GetByTestId(ImportTestIds.Test).BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);
        (await Page.GetByTestId(ImportTestIds.OpenImport).BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);
        (await TouchRules.ViolationsAsync(Page, tab)).ShouldBeEmpty();
    }

    [Test]
    public async Task Demos_WhenSavedOnAPhone_ShouldSaveFromThePanel()
    {
        var demos = new DemoSettingsTab(Page);
        await demos.GotoAsync("SHIP");
        await Expect(demos.Panel).ToBeVisibleAsync();

        (await demos.Environment.EvaluateAsync<string>("el => getComputedStyle(el).fontSize")).ShouldBe("16px");
        await Expect(Page.GetByTestId(TouchTestIds.SaveBar)).ToHaveCountAsync(0);
        await demos.Environment.FillAsync("make seed && make run");

        var panelWidth = await demos.Panel.EvaluateAsync<double>("el => el.getBoundingClientRect().width");
        var save = await demos.Save.BoundingBoxAsync();
        save!.Height.ShouldBeGreaterThanOrEqualTo(32);
        ((double)save.Width).ShouldBeGreaterThan(panelWidth - 40);

        await Page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/demo-settings", "PUT", () => demos.Save.TapAsync());
        await Page.ReloadAsync();
        await Expect(demos.Environment).ToHaveValueAsync("make seed && make run");
        (await TouchRules.ViolationsAsync(Page, demos.Panel)).ShouldBeEmpty();
    }
}
