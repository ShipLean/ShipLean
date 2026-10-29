using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The Capacity page: the velocity, a row per member in the table (days, capacity, note), the team total and the sprint as a whole.
public sealed class CapacityPage(IPage page)
{
    public ILocator Rows => page.GetByTestId(SprintsTestIds.CapacityRow);

    public ILocator Row(string member) => Rows.Filter(new() { HasText = member });

    // The member's computed capacity (read-only text).
    public ILocator Points(string member) => Row(member).GetByTestId(SprintsTestIds.CapacityPoints);

    public ILocator Days(string member) => Row(member).GetByTestId(SprintsTestIds.CapacityDays);

    public ILocator DefaultMarker(string member) => Row(member).GetByTestId(SprintsTestIds.CapacityDaysDefault);

    public ILocator Reset(string member) => Row(member).GetByTestId(SprintsTestIds.CapacityDaysReset);

    public ILocator Remove(string member) => Row(member).GetByTestId(SprintsTestIds.CapacityRemove);

    public ILocator Velocity => page.GetByTestId(SprintsTestIds.CapacityVelocity);

    public ILocator Calendar => page.GetByTestId(SprintsTestIds.CapacityCalendar);

    public ILocator Add => page.GetByTestId(SprintsTestIds.CapacityAdd);

    public ILocator Empty => page.GetByTestId(SprintsTestIds.CapacityEmpty);

    public ILocator Note(string member) => Row(member).GetByTestId(SprintsTestIds.CapacityNote);

    public ILocator Total => page.GetByTestId(SprintsTestIds.CapacityTotal);

    public ILocator KpiCapacity => page.GetByTestId(SprintsTestIds.CapacityKpiCapacity);

    public ILocator KpiCommitted => page.GetByTestId(SprintsTestIds.CapacityKpiCommitted);

    public ILocator KpiVelocity => page.GetByTestId(SprintsTestIds.CapacityKpiVelocity);

    public ILocator Room => page.GetByTestId(SprintsTestIds.CapacityRoom);

    public ILocator Callout => page.GetByTestId(SprintsTestIds.CapacityCallout);

    public ILocator Copy => page.GetByTestId(SprintsTestIds.CapacityCopy);

    public ILocator Clear => page.GetByTestId(SprintsTestIds.CapacityClear);

    public ILocator SaveButton => page.GetByTestId(SprintsTestIds.CapacitySave);

    public async Task OpenAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.SprintCapacity(projectKey, sprintId));
        await page.GetByTestId(SprintsTestIds.Capacity).WaitForAsync();
        await Total.WaitForAsync();
    }

    // Presses Save and returns the capacity PUT response.
    public Task<IResponse> SaveAsync(Guid sprintId) =>
        page.RunAndWaitForApiAsync($"/api/sprints/{sprintId}/capacity", "PUT", () => SaveButton.ClickAsync());
}
