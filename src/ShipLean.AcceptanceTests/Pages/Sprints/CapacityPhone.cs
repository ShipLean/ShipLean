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

    // Presses the bar's Save and returns the capacity PUT response.
    public Task<IResponse> SaveAsync(Guid sprintId) =>
        page.RunAndWaitForApiAsync($"/api/sprints/{sprintId}/capacity", "PUT", () => Save.TapAsync());
}
