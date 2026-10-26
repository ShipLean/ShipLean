using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The Integrations tab of Instance settings.
public sealed class InstanceIntegrationsPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(SettingsTestIds.IntegrationsPanel);

    public ILocator GitLabUrl => page.GetByTestId(SettingsTestIds.IntegrationsGitLabUrl);

    public ILocator GitLabTokenInput => page.GetByTestId(SettingsTestIds.IntegrationsGitLabTokenInput);

    public ILocator GitLabTokenLastFour => page.GetByTestId(SettingsTestIds.IntegrationsGitLabTokenLastFour);

    public async Task OpenAsync() => await page.GotoAsync(Links.InstanceSettings("integrations"));

    // Saves and waits for the response, so a following reload does not abort the write.
    public async Task SaveAsync() =>
        await page.RunAndWaitForApiAsync("/api/instance/integrations", "PUT", () => page.GetByTestId(SettingsTestIds.IntegrationsSave).ClickAsync());
}
