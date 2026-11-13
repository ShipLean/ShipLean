using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Shell.Touch;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Touch;

// The touch drawer on a phone against the real server, signed in as Joel (SHIP-713): every section's sub-pages, the
// user menu at its bottom and the project picker as a bottom sheet over it. The touch rules are checked on the drawer,
// the user menu and the picker sheet only; the page content is other tasks'.
[TestFixture]
public class TouchDrawerTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Drawer_WhenEpicsTappedUnderBacklogs_ShouldOpenEpicsBacklogAndClose()
    {
        var drawer = new TouchDrawerPanel(Page);
        await Page.GotoAsync(Links.Board("SHIP", "stories"));
        await Expect(Page.GetByTestId(PagesTestIds.BoardPage)).ToBeVisibleAsync();

        await drawer.Touch.OpenDrawerAsync();
        await Expect(drawer.Drawer.GetByTestId(ShellTestIds.NavAllSprints)).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, drawer.Drawer)).ShouldBeEmpty();
        await drawer.OpenUserMenuAsync();
        (await TouchRules.ViolationsAsync(Page, drawer.UserMenu)).ShouldBeEmpty();
        await drawer.UserButton.TapAsync();
        await Expect(drawer.UserMenu).ToHaveCountAsync(0);

        await drawer.BacklogLevel("Epics").TapAsync();

        await Expect(Page).ToHaveURLAsync(EndsWith(Links.Backlog("SHIP", "epics")));
        await Expect(drawer.Drawer).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Picker_WhenProjectPicked_ShouldOpenSameScreenInThatProjectAndCloseDrawer()
    {
        var drawer = new TouchDrawerPanel(Page);
        await Page.GotoAsync(Links.Board("SHIP", "stories"));
        await Expect(Page.GetByTestId(PagesTestIds.BoardPage)).ToBeVisibleAsync();
        await drawer.Touch.OpenDrawerAsync();

        await drawer.OpenPickerAsync();
        await Expect(drawer.PickerOption("PAY")).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, drawer.PickerSheet)).ShouldBeEmpty();
        await drawer.PickerOption("PAY").TapAsync();

        await Expect(Page).ToHaveURLAsync(EndsWith(Links.Board("PAY", "stories")));
        await Expect(Page.GetByTestId(PagesTestIds.BoardPage)).ToBeVisibleAsync();
        await Expect(drawer.Touch.Sheets).ToHaveCountAsync(0);
        await Expect(drawer.Drawer).ToHaveCountAsync(0);
    }

    private static Regex EndsWith(string pathAndQuery) =>
        new(Regex.Escape(pathAndQuery) + "$");
}
