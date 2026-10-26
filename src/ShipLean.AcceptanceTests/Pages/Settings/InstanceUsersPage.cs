using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The Users tab of Instance settings, and the password reveal that dialogs show.
public sealed class InstanceUsersPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(SettingsTestIds.UsersPanel);

    public ILocator Rows => page.GetByTestId(SettingsTestIds.UsersRow);

    public ILocator Row(string email) => Rows.Filter(new() { HasText = email });

    public ILocator StatusOf(string email) => Row(email).GetByTestId(SettingsTestIds.UsersStatus);

    public ILocator Password => page.GetByTestId(ComponentTestIds.OtpPassword);

    public ILocator DialogError => page.GetByTestId(SettingsTestIds.UsersDialogError);

    public async Task OpenAsync() => await page.GotoAsync(Links.InstanceSettings("users"));

    public async Task OpenMenuAsync(string email, string itemTestId)
    {
        await Row(email).GetByTestId(SettingsTestIds.UsersActions).ClickAsync();
        await page.GetByTestId(itemTestId).ClickAsync();
    }

    public async Task ShowMenuAsync(string email) =>
        await Row(email).GetByTestId(SettingsTestIds.UsersActions).ClickAsync();

    public ILocator MenuItem(string testId) => page.GetByTestId(testId);

    // Adds a user, ticking the first project the picker lists (EMPT: the caller's administered projects are in key
    // order), and waits for the response, so a following reload does not abort the write.
    public async Task AddAsync(string email, string name)
    {
        await page.GetByTestId(SettingsTestIds.UsersAdd).ClickAsync();
        await page.GetByTestId(SettingsTestIds.UsersAddEmail).FillAsync(email);
        await page.GetByTestId(SettingsTestIds.UsersAddName).FillAsync(name);
        await page.GetByTestId(SettingsTestIds.UsersAddProject).First.CheckAsync();
        await page.RunAndWaitForApiAsync("/api/admin/users", "POST", () => page.GetByTestId(SettingsTestIds.UsersAddSubmit).ClickAsync());
    }

    public async Task ConfirmAsync(string method, string path) =>
        await page.RunAndWaitForApiAsync(path, method, () => page.GetByTestId(SettingsTestIds.UsersDialogConfirm).ClickAsync());
}
