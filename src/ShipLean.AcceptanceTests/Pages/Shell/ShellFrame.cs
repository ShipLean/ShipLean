using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Shell;

// The frame every page after sign-in sits in: the side menu (project button and picker, user menu) and the top bar.
public sealed class ShellFrame(IPage page)
{
    public ILocator SideMenu => page.GetByTestId(ShellTestIds.SideMenu);

    public ILocator TopBar => page.GetByTestId(AppTestIds.ShellTopBar);

    public ILocator Main => page.GetByTestId(AppTestIds.ShellMain);

    public ILocator ProjectButton => page.GetByTestId(ShellTestIds.ProjectButton);

    public ILocator Picker => page.GetByTestId(ShellTestIds.Picker);

    public ILocator PickerOptions => page.GetByTestId(ShellTestIds.PickerOption);

    public ILocator UserButton => page.GetByTestId(ShellTestIds.UserButton);

    public ILocator UserMenu => page.GetByTestId(ShellTestIds.UserMenu);

    public ILocator SignOut => page.GetByTestId(ShellTestIds.UserMenuSignOut);

    public ILocator Crumbs => page.GetByTestId(ShellTestIds.Crumb);

    public static string BacklogPath(string key) => $"/p/{Uri.EscapeDataString(key)}/backlogs/stories";

    public async Task OpenAsync(string path)
    {
        await page.GotoAsync(path);
        await SideMenu.WaitForAsync();
    }

    public async Task OpenPickerAsync()
    {
        await ProjectButton.ClickAsync();
        await Picker.WaitForAsync();
    }

    public async Task SignOutAsync()
    {
        await UserButton.ClickAsync();
        await SignOut.ClickAsync();
    }
}
