using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Instance;

// An instance admin turns web vitals on in the Analytics tab; after a real save and a reload the switch is still on.
[TestFixture]
public class AnalyticsWebVitalsTests : SeededAcceptanceTest
{
    [Test]
    public async Task AnalyticsTab_WhenWebVitalsTurnedOn_ShouldSurviveAReload()
    {
        await Page.GotoAsync(Links.InstanceSettings("analytics"));
        var toggle = Page.GetByTestId(SettingsTestIds.AnalyticsSendWebVitalsToggle);

        await Expect(toggle).Not.ToBeCheckedAsync();
        await toggle.CheckAsync();
        var saved = await Page.RunAndWaitForApiAsync("/api/instance/analytics", "PUT", () => Page.GetByTestId(SettingsTestIds.AnalyticsSaveButton).ClickAsync());
        saved.Ok.ShouldBeTrue();
        saved.Request.PostData.ShouldNotBeNull().ShouldContain("\"sendWebVitals\":true");
        await Page.ReloadAsync();

        await Expect(Page.GetByTestId(SettingsTestIds.AnalyticsContainer)).ToBeVisibleAsync();
        await Expect(toggle).ToBeCheckedAsync();
    }
}
