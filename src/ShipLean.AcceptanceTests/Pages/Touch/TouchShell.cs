using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Touch;

// The shared touch pieces at 900px and below (K4-K7): the top bar, the drawer, sheets and action sheets, the Save bar,
// the full-screen dialog's touch controls, the pager and the image viewer. Use it from a class whose viewport is
// AcceptanceViewport.Phone; every action taps.
public sealed class TouchShell(IPage page)
{
    // K4 top bar and drawer
    public ILocator TopBar => page.GetByTestId(TouchTestIds.TopBar);

    public ILocator TopBarMenu => page.GetByTestId(TouchTestIds.TopBarMenu);

    public ILocator TopBarBack => page.GetByTestId(TouchTestIds.TopBarBack);

    public ILocator TopBarTitle => page.GetByTestId(TouchTestIds.TopBarTitle);

    public ILocator TopBarAction => page.GetByTestId(TouchTestIds.TopBarAction);

    public ILocator TopBarSearch => page.GetByTestId(TouchTestIds.TopBarSearch);

    public ILocator TopBarPinned => page.GetByTestId(TouchTestIds.TopBarPinned);

    public ILocator Drawer => page.GetByTestId(TouchTestIds.Drawer);

    public ILocator DrawerScrim => page.GetByTestId(TouchTestIds.DrawerScrim);

    public ILocator DrawerClose => page.GetByTestId(TouchTestIds.DrawerClose);

    // K5 overlays. Sheets and their parts can be open more than once (a sheet inside a full-screen dialog), so these
    // match every open one; Sheet(title) picks one.
    public ILocator Sheets => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SheetScrim => page.GetByTestId(TouchTestIds.SheetScrim);

    public ILocator SheetGrab => page.GetByTestId(TouchTestIds.SheetGrab);

    public ILocator SheetTitle => page.GetByTestId(TouchTestIds.SheetTitle);

    public ILocator SheetClose => page.GetByTestId(TouchTestIds.SheetClose);

    public ILocator SheetBody => page.GetByTestId(TouchTestIds.SheetBody);

    public ILocator SheetFooter => page.GetByTestId(TouchTestIds.SheetFooter);

    public ILocator ActionSheetRows => page.GetByTestId(TouchTestIds.ActionSheetRow);

    public ILocator MoreButton => page.GetByTestId(TouchTestIds.MoreButton);

    public ILocator SaveBar => page.GetByTestId(TouchTestIds.SaveBar);

    public ILocator SaveBarMessage => page.GetByTestId(TouchTestIds.SaveBarMessage);

    public ILocator SaveBarDiscard => page.GetByTestId(TouchTestIds.SaveBarDiscard);

    public ILocator SaveBarSave => page.GetByTestId(TouchTestIds.SaveBarSave);

    // K6 pager and touch drag
    public ILocator Pager => page.GetByTestId(TouchTestIds.Pager);

    public ILocator PagerChips => page.GetByTestId(TouchTestIds.PagerChips);

    public ILocator PagerChip => page.GetByTestId(TouchTestIds.PagerChip);

    public ILocator PagerPages => page.GetByTestId(TouchTestIds.PagerPage);

    public ILocator DragGhost => page.GetByTestId(TouchTestIds.DragGhost);

    // K7 markdown and image viewer
    public ILocator MarkdownScrolls => page.GetByTestId(TouchTestIds.MarkdownScroll);

    public ILocator ImageViewer => page.GetByTestId(TouchTestIds.ImageViewer);

    public ILocator ImageViewerImage => page.GetByTestId(TouchTestIds.ImageViewerImage);

    public ILocator ImageViewerClose => page.GetByTestId(TouchTestIds.ImageViewerClose);

    public async Task OpenDrawerAsync()
    {
        await TopBarMenu.TapAsync();
        await Drawer.WaitForAsync();
    }

    public async Task CloseDrawerAsync()
    {
        await DrawerClose.TapAsync();
        await Assertions.Expect(Drawer).ToBeHiddenAsync();
    }

    // The open sheet whose title is exactly this text.
    public ILocator Sheet(string title) =>
        Sheets.Filter(new LocatorFilterOptions { Has = page.GetByTestId(TouchTestIds.SheetTitle).Filter(new LocatorFilterOptions { HasTextRegex = Exact(title) }) });

    // Closes the topmost open sheet with its Close button.
    public async Task CloseSheetAsync()
    {
        var open = await Sheets.CountAsync();
        await SheetClose.Last.TapAsync();
        await Assertions.Expect(Sheets).ToHaveCountAsync(open - 1);
    }

    public ILocator ActionRow(string label) =>
        ActionSheetRows.Filter(new LocatorFilterOptions { HasTextRegex = Exact(label) });

    // The full-screen dialog's touch ✕ and primary action; the desktop × keeps ComponentTestIds.DialogClose.
    public static ILocator DialogClose(ILocator dialog) => dialog.GetByTestId(TouchTestIds.DialogClose);

    public static ILocator DialogPrimary(ILocator dialog) => dialog.GetByTestId(TouchTestIds.DialogPrimary);

    // The pager chip whose label starts with this text (a chip may add its count/limit after the label).
    public ILocator Chip(string label) =>
        PagerChip.Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^\\s*" + Regex.Escape(label) + "\\b") });

    private static Regex Exact(string text) => new("^\\s*" + Regex.Escape(text) + "\\s*$");
}
