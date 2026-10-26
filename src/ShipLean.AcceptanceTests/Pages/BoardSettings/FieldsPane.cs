using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.BoardSettings;

// The board settings dialog's Fields pane. The seeded Storefront stories board shows User Story (Parent, Priority,
// Sprint) first and Bug (Parent, Severity, Priority) second.
public sealed class FieldsPane(IPage page)
{
    public ILocator Pane => page.GetByTestId(BoardSettingsTestIds.FieldsPane);

    public ILocator Preview => page.GetByTestId(BoardSettingsTestIds.FieldsPreview);

    // Scoped to the preview: the board behind the dialog draws the same card extras.
    public ILocator PreviewFields => Preview.GetByTestId(BoardTestIds.CardExtra);

    public ILocator ExtraCount => page.GetByTestId(BoardSettingsTestIds.FieldsExtraCount);

    public ILocator ExtraRows => page.GetByTestId(BoardSettingsTestIds.FieldsExtraRow);

    public ILocator ShowKey => page.GetByTestId(BoardSettingsTestIds.FieldsShowKey);

    public ILocator ShowEmpty => page.GetByTestId(BoardSettingsTestIds.FieldsShowEmpty);

    public ILocator Add => page.GetByTestId(BoardSettingsTestIds.FieldsAdd);

    public ILocator OverLimit => page.GetByTestId(BoardSettingsTestIds.FieldsOverLimit);

    public ILocator SaveError => page.GetByTestId(SettingsDialogTestIds.FooterError);

    public async Task OpenAsync(string projectKey) =>
        await page.GotoAsync(Links.BoardSettings(projectKey, "stories", "fields"));

    public async Task SaveAsync() => await page.GetByTestId(SettingsDialogTestIds.Save).ClickAsync();
}
