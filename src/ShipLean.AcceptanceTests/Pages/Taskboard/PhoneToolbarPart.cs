using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Taskboard;

// The shared Taskboard toolbar at 900px and below (SHIP-722): Stories | People, Filter (n), ⋯ More and + Add on one row.
// The Feature board and the sprint Taskboard both draw it.
public sealed class PhoneToolbarPart(IPage page)
{
    public ILocator Toolbar => page.GetByTestId(TaskboardTestIds.Toolbar);

    public ILocator FilterButton => page.GetByTestId(TaskboardTestIds.FilterButton);

    public ILocator MoreButton => Toolbar.GetByTestId(TouchTestIds.MoreButton);

    public ILocator AddButton => page.GetByTestId(TaskboardTestIds.AddStory);

    public ILocator SheetRows => page.GetByTestId(TaskboardTestIds.FilterSheetRow);

    public ILocator SheetClose => page.GetByTestId(TouchTestIds.SheetClose);

    // A person's row in the open Filter sheet, by name.
    public ILocator SheetRow(string name) => SheetRows.Filter(new() { HasText = name });

    // Opens the Filter sheet and waits for its rows.
    public async Task OpenFilterAsync()
    {
        await FilterButton.TapAsync();
        await SheetRows.First.WaitForAsync();
    }

    // Opens + Add and types a title, then presses Enter; returns the create response.
    public async Task<IResponse> AddStoryAsync(string title)
    {
        await AddButton.TapAsync();
        var box = page.GetByTestId(TaskboardTestIds.AddStoryTitle);
        await box.FillAsync(title);
        return await page.RunAndWaitForApiAsync("/api/work-items", "POST", () => box.PressAsync("Enter"));
    }
}
