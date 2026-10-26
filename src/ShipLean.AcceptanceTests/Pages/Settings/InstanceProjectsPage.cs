using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The Projects tab of Instance settings.
public sealed class InstanceProjectsPage(IPage page)
{
    public ILocator Rows => page.GetByTestId(SettingsTestIds.ProjectsRow);

    // Matches the key cell exactly, so ARCH never picks up a project whose name merely contains it.
    public ILocator Row(string key) => Rows.Filter(new() { Has = page.GetByTestId(SettingsTestIds.ProjectsKey).Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(key)}$") }) });

    public ILocator Banners => page.GetByTestId(SettingsTestIds.ProjectsBanner);

    public async Task OpenAsync() => await page.GotoAsync(Links.InstanceSettings("projects"));

    // Joins from the row's button and waits for the response, so a following read sees the membership.
    public async Task JoinAsync(string key, Guid projectId) =>
        await page.RunAndWaitForApiAsync($"/api/instance/projects/{projectId}/join", "POST", () => Row(key).GetByTestId(SettingsTestIds.ProjectsJoin).ClickAsync());
}
