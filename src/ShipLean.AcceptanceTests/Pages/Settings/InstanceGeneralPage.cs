using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The General tab of Instance settings, and the refusal a non-admin sees on any instance tab.
public sealed class InstanceGeneralPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(SettingsTestIds.InstanceGeneralPanel);

    public ILocator BaseUrl => page.GetByTestId(SettingsTestIds.InstanceBaseUrl);

    public ILocator UrlMismatch => page.GetByTestId(SettingsTestIds.InstanceUrlMismatch);

    public ILocator UrlMatch => page.GetByTestId(SettingsTestIds.InstanceUrlMatch);

    public ILocator Denied => page.GetByTestId(SettingsTestIds.InstanceDenied);

    public async Task OpenAsync() => await page.GotoAsync(Links.InstanceSettings("general"));

    // Saves and waits for the response, so a following reload does not abort the write.
    public async Task SaveAsync() =>
        await page.RunAndWaitForApiAsync("/api/instance/settings", "PUT", () => page.GetByTestId(SettingsTestIds.InstanceSave).ClickAsync());
}
