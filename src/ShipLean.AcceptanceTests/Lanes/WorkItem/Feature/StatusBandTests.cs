using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

// Still true and the band editor write Features, so each test takes its own seeded Feature.
[TestFixture]
public class StatusBandTests : SeededAcceptanceTest
{
    [Test]
    public async Task StillTrue_WhenClicked_ShouldShowVerifiedJustNowAndAddHistoryRevision()
    {
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);
        var band = new StatusBandPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");
        await Expect(side.Revisions.First).ToBeVisibleAsync();
        var before = await side.Revisions.CountAsync();

        var confirmed = await band.ConfirmAsync(SeedIds.WorkItems.Ship101);

        confirmed.Ok.ShouldBeTrue();
        await Expect(band.Verified).ToContainTextAsync("Verified just now");
        await Expect(side.Revisions).ToHaveCountAsync(before + 1);
    }

    [Test]
    public async Task EditStatus_WhenSaved_ShouldShowNewStatusLine()
    {
        var screen = new WorkItemScreen(Page);
        var band = new StatusBandPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-102");

        await band.EditStatus.ClickAsync();
        await band.EditLine.FillAsync("Saved cards are on staging.");
        await band.Done.ClickAsync();
        var saved = await screen.SaveAsync(SeedIds.WorkItems.Ship102);

        saved.Ok.ShouldBeTrue();
        await Expect(band.StatusLine).ToHaveTextAsync("Saved cards are on staging.");
        await Expect(band.Verified).ToContainTextAsync("Verified just now");
        await Expect(band.StillTrue).ToBeEnabledAsync();
    }
}
