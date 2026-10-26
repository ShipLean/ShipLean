using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.BoardSettings;

// The board settings dialog's Styles pane. Rules are addressed by position: the seeded Storefront stories board has
// Blocked (tags contain Blocked), then Spike (tags contain Spike). The preview's first sample card is SHIP's
// "Sign in with passkey": tagged Blocked, priority 1.
public sealed class StylesPane(IPage page)
{
    public ILocator Pane => page.GetByTestId(BoardSettingsTestIds.StylesPane);

    public ILocator Rows => page.GetByTestId(BoardSettingsTestIds.StyleRow);

    public ILocator Names => page.GetByTestId(BoardSettingsTestIds.StyleName);

    public ILocator Add => page.GetByTestId(BoardSettingsTestIds.StylesAdd);

    // One line per sample card, saying which rule it got.
    public ILocator PreviewCaptions => page.GetByTestId(BoardSettingsTestIds.StylesPreviewCaption);

    public ILocator Grip(int rule) => page.GetByTestId(BoardSettingsTestIds.StyleGrip).Nth(rule);

    public ILocator ClauseField(int rule) =>
        Criteria(rule).GetByTestId(CriteriaTestIds.Field).First;

    public ILocator ClauseValue(int rule) =>
        Criteria(rule).GetByTestId(CriteriaTestIds.Value).First;

    private ILocator Criteria(int rule) => page.GetByTestId(BoardSettingsTestIds.StyleCriteria).Nth(rule);

    public async Task OpenAsync(string projectKey) =>
        await page.GotoAsync(Links.BoardSettings(projectKey, "stories", "styles"));

    public async Task SaveAndWaitAsync()
    {
        await page.GetByTestId(SettingsDialogTestIds.Save).ClickAsync();
        await Assertions.Expect(Pane).ToBeHiddenAsync();
    }
}
