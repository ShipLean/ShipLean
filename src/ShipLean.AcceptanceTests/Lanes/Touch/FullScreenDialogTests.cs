using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Touch;

// Every Dialog goes full screen at 900px and below (K5, SHIP-805 C9), shown on New Feature from the Features backlog of
// the seeded Storefront project (SHIP). This class creates nothing.
[TestFixture]
public class FullScreenDialogTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task NewFeatureDialog_WhenOpenedOnAPhone_ShouldFillTheScreenAndCloseWithTheCross()
    {
        await new LevelBacklog(Page).OpenAsync("SHIP", "features");
        await new NewFeatureDialog(Page).OpenAsync();
        var dialog = Page.GetByTestId(ComponentTestIds.Dialog);

        var box = await dialog.BoundingBoxAsync();
        box.ShouldNotBeNull();
        (box.X, box.Y, box.Width, box.Height).ShouldBe((0f, 0f, 390f, 844f));
        await Expect(dialog.GetByTestId(ComponentTestIds.DialogClose)).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, TouchShell.DialogClose(dialog))).ShouldBeEmpty();

        await TouchShell.DialogClose(dialog).TapAsync();

        await Expect(dialog).ToHaveCountAsync(0);
    }
}
