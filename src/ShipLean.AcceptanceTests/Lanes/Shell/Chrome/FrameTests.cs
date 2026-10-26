using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Chrome;

// The frame against the real server, signed in as Joel (a member of every seeded project except PART).
[TestFixture]
public class FrameTests : SeededAcceptanceTest
{
    [Test]
    public async Task Shell_WhenNarrow_ShouldShowStrip()
    {
        var frame = new ShellFrame(Page);
        await Page.SetViewportSizeAsync(800, 700);

        await frame.OpenAsync(ShellFrame.BacklogPath("SHIP"));

        var side = (await frame.SideMenu.BoundingBoxAsync())!;
        var main = (await frame.Main.BoundingBoxAsync())!;
        side.Width.ShouldBeGreaterThan(780);
        side.Height.ShouldBeLessThan(200);
        main.Y.ShouldBeGreaterThanOrEqualTo(side.Y + side.Height);
    }

    [Test]
    public async Task Shell_WhenWide_ShouldShowSideMenuBesideTheTopBar()
    {
        var frame = new ShellFrame(Page);

        await frame.OpenAsync(ShellFrame.BacklogPath("SHIP"));

        var side = (await frame.SideMenu.BoundingBoxAsync())!;
        var top = (await frame.TopBar.BoundingBoxAsync())!;
        side.Width.ShouldBeLessThan(300);
        top.X.ShouldBeGreaterThanOrEqualTo(side.X + side.Width);
        await Expect(frame.Crumbs).ToHaveTextAsync(["SHIP", "Backlogs", "Stories"]);
    }

    [Test]
    public async Task Picker_WhenNotMember_ShouldNotListProject()
    {
        var frame = new ShellFrame(Page);
        await frame.OpenAsync(ShellFrame.BacklogPath("SHIP"));

        await frame.OpenPickerAsync();

        await Expect(frame.PickerOptions.Filter(new() { HasText = "SHIP" })).ToHaveCountAsync(1);
        await Expect(frame.PickerOptions.Filter(new() { HasText = "PART" })).ToHaveCountAsync(0);
        await Expect(frame.PickerOptions.Filter(new() { HasText = "Partner Portal" })).ToHaveCountAsync(0);
    }
}
