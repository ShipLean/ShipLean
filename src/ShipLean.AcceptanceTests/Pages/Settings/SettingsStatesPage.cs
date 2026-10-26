using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The States tab of Storefront's project settings, on the User Story type. Rows are by position: the seeded story states
// are New, Active, Resolved, Closed, Removed.
public sealed class SettingsStatesPage(IPage page)
{
    public ILocator Rows => page.GetByTestId(SettingsTestIds.StateRow);

    public ILocator Unsaved => page.GetByTestId(SettingsTestIds.Unsaved);

    public ILocator SaveButton => page.GetByTestId(SettingsTestIds.Save);

    public ILocator SaveError => page.GetByTestId(SettingsTestIds.SaveError);

    public ILocator DeleteForm => page.GetByTestId(SettingsTestIds.StateDeleteForm);

    public ILocator DeleteConfirm => page.GetByTestId(SettingsTestIds.StateDeleteConfirm);

    public ILocator NameOf(int row) => Rows.Nth(row).GetByTestId(SettingsTestIds.StateName);

    public ILocator CategoryOf(int row) => Rows.Nth(row).GetByTestId(SettingsTestIds.StateCategory);

    public ILocator DotOf(int row) => Rows.Nth(row).GetByTestId(ComponentTestIds.StateDot);

    public ILocator ItemsOf(int row) => Rows.Nth(row).GetByTestId(SettingsTestIds.StateItems);

    public ILocator DeleteOf(int row) => Rows.Nth(row).GetByTestId(SettingsTestIds.StateDelete);

    public ILocator DeleteBlockOf(int row) => Rows.Nth(row).GetByTestId(SettingsTestIds.StateDeleteBlock);

    public async Task OpenAsync() => await page.GotoAsync(Links.ProjectSettings("SHIP", "states"));

    public async Task ChooseTargetAsync(string stateName) =>
        await page.GetByTestId(SettingsTestIds.StateDeleteTarget).SelectOptionAsync(new SelectOptionValue { Label = stateName });

    // Saves and waits for the response, so a following reload does not abort the write.
    public async Task<IResponse> SaveAsync() =>
        await page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/settings", "PUT", () => SaveButton.ClickAsync());
}
