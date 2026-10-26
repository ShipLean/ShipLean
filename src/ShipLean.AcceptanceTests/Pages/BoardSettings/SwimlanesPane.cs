using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.BoardSettings;

// The board settings dialog's Swimlanes pane. Lanes are addressed by position: the seeded Storefront stories board has
// Expedite (criteria: priority = 1), then the Default lane.
public sealed class SwimlanesPane(IPage page)
{
    public ILocator Pane => page.GetByTestId(BoardSettingsTestIds.SwimlanesPane);

    public ILocator Rows => page.GetByTestId(BoardSettingsTestIds.LaneRow);

    public ILocator Names => page.GetByTestId(BoardSettingsTestIds.LaneName);

    public ILocator Refusal => page.GetByTestId(BoardSettingsTestIds.LaneRefusal);

    public ILocator Add => page.GetByTestId(BoardSettingsTestIds.LanesAdd);

    public ILocator Delete(int lane) => page.GetByTestId(BoardSettingsTestIds.LaneDelete).Nth(lane);

    // Only lanes that are not the default one have the Criteria / Manual switch.
    public ILocator CriteriaMode(int switchIndex) => page.GetByTestId(BoardSettingsTestIds.LaneModeCriteria).Nth(switchIndex);

    // The value select of the first clause of the nth criteria lane (manual and default lanes have none).
    public ILocator ClauseValue(int lane) =>
        page.GetByTestId(BoardSettingsTestIds.LaneCriteria).Nth(lane).GetByTestId(CriteriaTestIds.Value).First;

    public async Task OpenAsync(string projectKey) =>
        await page.GotoAsync(Links.BoardSettings(projectKey, "stories", "swimlanes"));

    public async Task SaveAndWaitAsync()
    {
        await page.GetByTestId(SettingsDialogTestIds.Save).ClickAsync();
        await Assertions.Expect(Pane).ToBeHiddenAsync();
    }
}
