using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// The Feature's status band, fields, Goal and Stories and bugs on a phone against the real server (SHIP-714, L714e).
// The touch rules are checked on this task's regions only; the header, links and log are other tasks'.
[TestFixture]
public class FeatureTopTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Status_WhenBlockedWithReasonOnPhone_ShouldMarkStatusUnsaved()
    {
        var screen = new WorkItemScreen(Page);
        var band = new StatusBandPanel(Page);
        var strip = new FeatureStripPanel(Page);
        var touch = new TouchShell(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");
        (await TouchRules.ViolationsAsync(Page, band.Band)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, strip.Strip)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, strip.Goal)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, strip.StoriesAndBugs)).ShouldBeEmpty();

        await band.EditStatus.TapAsync();

        await Expect(band.Editor).ToBeVisibleAsync();
        await Expect(band.EditHint).ToContainTextAsync("Save writes it");
        await Expect(band.EditHint).Not.ToContainTextAsync("in the header");
        await band.EditLine.FillAsync("Blocked on the card processor's sandbox.");
        await band.Done.TapAsync();
        await band.HealthSelect.SelectOptionAsync("blocked");

        await Expect(band.StatusLine).ToHaveTextAsync("Blocked on the card processor's sandbox.");
        await Expect(touch.SaveBarMessage).ToContainTextAsync("Status");
        await Expect(touch.SaveBarMessage).ToContainTextAsync("Health");
        (await TouchRules.ViolationsAsync(Page, band.Band)).ShouldBeEmpty();
    }
}
