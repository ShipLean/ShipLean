using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.QuickSearch;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Touch;

// The touch shell (K4) on a phone against the real server, signed in as Joel: the pinned top bar, the side menu in a
// drawer, and quick search from the bar. The touch rules are checked on the bar and the open drawer only; the page
// content is other tasks' (the route sweep covers the whole screen).
[TestFixture]
public class TouchFrameTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Shell_WhenOnAPhone_ShouldShowTheTouchTopBarAndNoSideMenu()
    {
        var frame = new ShellFrame(Page);
        var touch = new TouchShell(Page);

        await Page.GotoAsync(ShellFrame.BacklogPath("SHIP"));

        await Expect(touch.TopBar).ToBeVisibleAsync();
        await Expect(touch.TopBarTitle.GetByTestId(ShellTestIds.Crumb)).ToHaveTextAsync(["SHIP", "Backlogs", "Stories"]);
        await Expect(frame.SideMenu).ToHaveCountAsync(0);
        await Expect(frame.TopBar).ToHaveCountAsync(0);
        await Expect(touch.Drawer).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, touch.TopBar)).ShouldBeEmpty();
    }

    [Test]
    public async Task Shell_WhenMenuTappedOnAPhone_ShouldOpenTheDrawerAndNavigate()
    {
        var frame = new ShellFrame(Page);
        var touch = new TouchShell(Page);
        await Page.GotoAsync(ShellFrame.BacklogPath("SHIP"));
        await Expect(touch.TopBar).ToBeVisibleAsync();

        await touch.OpenDrawerAsync();

        await Expect(touch.Drawer.GetByTestId(ShellTestIds.SideMenu)).ToBeVisibleAsync();
        await Expect(touch.DrawerScrim).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, touch.Drawer)).ShouldBeEmpty();

        await touch.Drawer.GetByTestId(ShellTestIds.NavSprints).TapAsync();

        await Expect(new AllSprints(Page).Rows.First).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/p/SHIP/sprints$"));
        await Expect(touch.Drawer).ToHaveCountAsync(0);
        await Expect(touch.DrawerScrim).ToHaveCountAsync(0);
        await Expect(frame.SideMenu).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Shell_WhenSearchTappedOnAPhone_ShouldOpenQuickSearch()
    {
        var touch = new TouchShell(Page);
        var search = new QuickSearchDialog(Page);
        await Page.GotoAsync(ShellFrame.BacklogPath("SHIP"));
        await Expect(touch.TopBar).ToBeVisibleAsync();

        await touch.TopBarSearch.TapAsync();

        await Expect(search.Dialog).ToBeVisibleAsync();
        await Expect(search.Action("Create bug")).ToBeVisibleAsync();
    }
}
