using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Shell.Touch;

// The side menu inside the touch drawer (SHIP-713): its sections and sub-pages, the project picker sheet over the
// drawer and the user menu at the drawer's bottom. Use it from a class whose viewport is AcceptanceViewport.Phone;
// every action taps.
public sealed class TouchDrawerPanel(IPage page)
{
    public const string PickerTitle = "Switch project";

    public TouchShell Touch { get; } = new(page);

    public ILocator Drawer => Touch.Drawer;

    public ILocator ProjectButton => Drawer.GetByTestId(ShellTestIds.ProjectButton);

    public ILocator UserButton => Drawer.GetByTestId(ShellTestIds.UserButton);

    public ILocator UserMenu => Drawer.GetByTestId(ShellTestIds.UserMenu);

    public ILocator PickerSheet => Touch.Sheet(PickerTitle);

    // Epics, Features or Stories under Backlogs (the drawer lists Backlogs' levels before Boards').
    public ILocator BacklogLevel(string label) => Levels(label).Nth(0);

    // Epics, Features or Stories under Boards.
    public ILocator BoardLevel(string label) => Levels(label).Nth(1);

    // The picker sheet's option for a project, by its key (the option's text starts with the key badge).
    public ILocator PickerOption(string key) =>
        PickerSheet.GetByTestId(ShellTestIds.PickerOption).Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^" + Regex.Escape(key)) });

    public async Task OpenPickerAsync()
    {
        await ProjectButton.TapAsync();
        await PickerSheet.WaitForAsync();
    }

    public async Task OpenUserMenuAsync()
    {
        await UserButton.TapAsync();
        await UserMenu.WaitForAsync();
    }

    private ILocator Levels(string label) =>
        Drawer.GetByTestId(ShellTestIds.NavLevel).Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^\\s*" + Regex.Escape(label) + "\\s*$") });
}
