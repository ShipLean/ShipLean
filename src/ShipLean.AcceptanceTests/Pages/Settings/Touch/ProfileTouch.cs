using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings.Touch;

// Your profile at 900px and below (SHIP-733): the identity form with the shared Save bar, the closed Password panel, and
// Appearance.
public sealed class ProfileTouch(IPage page)
{
    public ILocator IdentityPanel => page.GetByTestId(SettingsTestIds.ProfileIdentityPanel);

    public ILocator PasswordPanel => page.GetByTestId(SettingsTestIds.PasswordPanel);

    public ILocator AppearancePanel => page.GetByTestId(SettingsTestIds.AppearancePanel);

    public ILocator Name => page.GetByTestId(SettingsTestIds.ProfileName);

    // The third identity colour, #7b4fc9; the seeded user's own colour is not one of the palette.
    public ILocator PurpleSwatch => page.GetByTestId(SettingsTestIds.ProfileColour).Nth(2);

    // The panel's own footer, which hides at this size.
    public ILocator PanelFooterSave => page.GetByTestId(SettingsTestIds.ProfileSave);

    public ILocator SaveBar => page.GetByTestId(TouchTestIds.SaveBar);

    public ILocator SaveBarMessage => page.GetByTestId(TouchTestIds.SaveBarMessage);

    public ILocator SaveBarSave => page.GetByTestId(TouchTestIds.SaveBarSave);

    public ILocator PasswordOpen => page.GetByTestId(SettingsTestIds.PasswordOpen);

    public ILocator PasswordCancel => page.GetByTestId(SettingsTestIds.PasswordCancel);

    public ILocator PasswordForm => page.GetByTestId(SettingsTestIds.PasswordForm);

    public ILocator PasswordCurrent => page.GetByTestId(SettingsTestIds.PasswordCurrent);

    public ILocator PasswordNew => page.GetByTestId(SettingsTestIds.PasswordNew);

    public ILocator PasswordConfirm => page.GetByTestId(SettingsTestIds.PasswordConfirm);

    public ILocator PasswordSubmit => page.GetByTestId(SettingsTestIds.PasswordSubmit);

    public ILocator Toasts => page.GetByTestId(ComponentTestIds.Toast);

    public ILocator DensityOptions => page.GetByTestId(SettingsTestIds.DensityOption);

    public ILocator ThemeOptions => page.GetByTestId(SettingsTestIds.ThemeOption);

    public async Task OpenAsync()
    {
        await page.GotoAsync(Links.Profile());
        await Assertions.Expect(IdentityPanel).ToBeVisibleAsync();
    }

    public static async Task<string> FontSizeAsync(ILocator element) =>
        await element.EvaluateAsync<string>("el => getComputedStyle(el).fontSize");
}
