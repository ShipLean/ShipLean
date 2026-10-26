using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The account panels of Your profile: password, appearance, your projects, sessions and saved views.
public sealed class ProfileAccountPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(SettingsTestIds.AccountPanel);

    public ILocator PasswordSubmit => page.GetByTestId(SettingsTestIds.PasswordSubmit);

    public ILocator PasswordError => page.GetByTestId(SettingsTestIds.PasswordError);

    public ILocator ResetViews => page.GetByTestId(SettingsTestIds.ResetViews);

    public ILocator LeaveConfirmError => page.GetByTestId(SettingsTestIds.MyProjectLeaveError);

    public ILocator ProjectRow(string key) =>
        page.GetByTestId(SettingsTestIds.MyProjectRow).Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{key}") });

    public async Task OpenAsync()
    {
        await page.GotoAsync(Links.Profile());
        await Assertions.Expect(Panel).ToBeVisibleAsync();
    }

    // Fills the form and waits for the change to finish, so nothing after it aborts the request.
    public async Task ChangePasswordAsync(string current, string next)
    {
        await page.GetByTestId(SettingsTestIds.PasswordCurrent).FillAsync(current);
        await page.GetByTestId(SettingsTestIds.PasswordNew).FillAsync(next);
        await page.GetByTestId(SettingsTestIds.PasswordConfirm).FillAsync(next);
        await page.RunAndWaitForApiAsync("/api/auth/password", "POST", () => PasswordSubmit.ClickAsync());
    }

    public async Task LeaveAsync(string key)
    {
        await ProjectRow(key).GetByTestId(SettingsTestIds.MyProjectLeave).ClickAsync();
        await page.RunAndWaitForResponseAsync(
            () => page.GetByTestId(SettingsTestIds.MyProjectLeaveConfirm).ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/leave", StringComparison.Ordinal));
    }

    public async Task ResetViewsAsync() =>
        await page.RunAndWaitForApiAsync("/api/me/preferences/reset-views", "POST", () => ResetViews.ClickAsync());
}
