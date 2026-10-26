using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The New project page.
public sealed class NewProjectPage(IPage page)
{
    public ILocator Name => page.GetByTestId(SettingsTestIds.NewProjectName);

    public ILocator Key => page.GetByTestId(SettingsTestIds.NewProjectKey);

    public ILocator KeyFree => page.GetByTestId(SettingsTestIds.NewProjectCheckFree);

    public ILocator Alternatives => page.GetByTestId(SettingsTestIds.NewProjectAlternative);

    public ILocator CreateButton => page.GetByTestId(SettingsTestIds.NewProjectCreate);

    public ILocator Blocked => page.GetByTestId(SettingsTestIds.NewProjectBlocked);

    public ILocator Form => page.GetByTestId(SettingsTestIds.NewProjectForm);

    public async Task OpenAsync() => await page.GotoAsync(Links.NewProject());

    public async Task SprintAsync(string start, string end)
    {
        await page.GetByTestId(SettingsTestIds.NewProjectSprintStart).FillAsync(start);
        await page.GetByTestId(SettingsTestIds.NewProjectSprintEnd).FillAsync(end);
    }

    public async Task CopyFromAsync(Guid projectId)
    {
        await page.GetByTestId(SettingsTestIds.NewProjectFromCopy).CheckAsync();
        await page.GetByTestId(SettingsTestIds.NewProjectCopySource).SelectOptionAsync(projectId.ToString());
    }

    // Clicks Create and waits for the POST to finish, so a following navigation does not abort the write.
    public async Task<IResponse> CreateAsync() =>
        await page.RunAndWaitForApiAsync("/api/projects", "POST", () => CreateButton.ClickAsync());
}
