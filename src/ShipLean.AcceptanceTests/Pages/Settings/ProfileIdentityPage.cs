using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The identity panel of Your profile: display name, photo, colour.
public sealed class ProfileIdentityPage(IPage page)
{
    // A 1x1 PNG, so a browser really decodes what the server serves.
    public static byte[] NewPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public ILocator AvatarPhoto => page.GetByTestId(SettingsTestIds.ProfileAvatarPhoto);

    // The third identity colour, #7b4fc9.
    public ILocator PurpleSwatch => page.GetByTestId(SettingsTestIds.ProfileColour).Nth(2);

    public ILocator Panel => page.GetByTestId(SettingsTestIds.ProfileIdentityPanel);

    public ILocator Name => page.GetByTestId(SettingsTestIds.ProfileName);

    public ILocator Avatar => page.GetByTestId(SettingsTestIds.ProfileAvatar);

    public ILocator PhotoError => page.GetByTestId(SettingsTestIds.ProfilePhotoError);

    public ILocator Save => page.GetByTestId(SettingsTestIds.ProfileSave);

    public ILocator ShellUserButton => page.GetByTestId(ShellTestIds.UserButton);

    public async Task OpenAsync()
    {
        await page.GotoAsync(Links.Profile());
        await Expect(Panel).ToBeVisibleAsync();
    }

    public async Task PickPhotoAsync(string fileName, byte[] bytes, string mimeType = "image/png") =>
        await page.GetByTestId(SettingsTestIds.ProfilePhotoInput).SetInputFilesAsync(new FilePayload { Name = fileName, MimeType = mimeType, Buffer = bytes });

    // Saves and waits for the write to finish, so nothing after it aborts it.
    public async Task SaveAsync(string method, string path) =>
        await page.RunAndWaitForApiAsync(path, method, () => Save.ClickAsync());

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
