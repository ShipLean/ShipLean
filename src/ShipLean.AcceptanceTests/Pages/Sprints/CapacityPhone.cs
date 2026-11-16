using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The Capacity page's touch parts (SHIP-726): the summary tiles, the Copy and ⋯ toolbar and the Save bar that replaces Save.
// Use with CapacityPage for the rows, the total and the velocity.
public sealed class CapacityPhone(IPage page)
{
    public ILocator Summary => page.GetByTestId(SprintsTestIds.CapacitySummary);

    public ILocator Toolbar => page.GetByTestId(SprintsTestIds.CapacityPhoneToolbar);

    public ILocator More => Toolbar.GetByTestId(TouchTestIds.MoreButton);

    public ILocator SaveBar => page.GetByTestId(TouchTestIds.SaveBar);

    public ILocator Discard => page.GetByTestId(TouchTestIds.SaveBarDiscard);

    public ILocator Save => page.GetByTestId(TouchTestIds.SaveBarSave);

    // A person's row (SHIP-1808 P2): "9 / 10 days · default" beside their bar.
    public ILocator RowDays(string member) =>
        page.GetByTestId(SprintsTestIds.CapacityRow).Filter(new() { HasText = member }).GetByTestId(SprintsTestIds.CapacityRowDays);

    // The days sheet a row opens (P3).
    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SheetDays => Sheet.GetByTestId(SprintsTestIds.CapacityDays);

    // Opens the member's days sheet, types the days and closes it again.
    public async Task SetDaysAsync(string member, string days)
    {
        await page.GetByTestId(SprintsTestIds.CapacityRow).Filter(new() { HasText = member }).TapAsync();
        await SheetDays.FillAsync(days);
        await Sheet.GetByTestId(TouchTestIds.SheetClose).TapAsync();
        await Assertions.Expect(Sheet).ToHaveCountAsync(0);
    }

    // Presses the bar's Save and returns the capacity PUT response.
    public Task<IResponse> SaveAsync(Guid sprintId) =>
        page.RunAndWaitForApiAsync($"/api/sprints/{sprintId}/capacity", "PUT", () => Save.TapAsync());
}
