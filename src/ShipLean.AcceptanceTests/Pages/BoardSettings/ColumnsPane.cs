using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.BoardSettings;

// The board settings dialog's Columns pane. Rows are addressed by position: the seeded Storefront stories board has
// New, Ready, In progress, In review, Done.
public sealed class ColumnsPane(IPage page)
{
    public ILocator Pane => page.GetByTestId(BoardSettingsTestIds.ColumnsPane);

    public ILocator Rows => page.GetByTestId(BoardSettingsTestIds.ColumnRow);

    public ILocator Blocked => page.GetByTestId(BoardSettingsTestIds.ColumnBlocked);

    public ILocator Names => page.GetByTestId(BoardSettingsTestIds.ColumnName);

    public ILocator Unmapped => page.GetByTestId(BoardSettingsTestIds.ColumnsUnmapped);

    public ILocator AllMapped => page.GetByTestId(BoardSettingsTestIds.ColumnsAllMapped);

    public ILocator Fix => page.GetByTestId(BoardSettingsTestIds.ColumnsFix);

    public ILocator SaveError => page.GetByTestId(SettingsDialogTestIds.FooterError);

    public ILocator BugState(int row) => page.GetByTestId(BoardSettingsTestIds.ColumnStateBug).Nth(row);

    public ILocator Grip(int row) => page.GetByTestId(BoardSettingsTestIds.ColumnGrip).Nth(row);

    public ILocator Delete(int row) => page.GetByTestId(BoardSettingsTestIds.ColumnDelete).Nth(row);

    public async Task OpenAsync(string projectKey) =>
        await page.GotoAsync(Links.BoardSettings(projectKey, "stories", "columns"));

    public ILocator Deleted => page.GetByTestId(BoardSettingsTestIds.ColumnDeleted);

    public ILocator UndoDelete => page.GetByTestId(BoardSettingsTestIds.ColumnUndoDelete);

    public async Task SaveAsync() => await page.GetByTestId(SettingsDialogTestIds.Save).ClickAsync();

    // Save closes the dialog once the server has stored the change; navigating before then would abort the request.
    public async Task SaveAndWaitAsync()
    {
        await SaveAsync();
        await Assertions.Expect(Pane).ToBeHiddenAsync();
    }
}
