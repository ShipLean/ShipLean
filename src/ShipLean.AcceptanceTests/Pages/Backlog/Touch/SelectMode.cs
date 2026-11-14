using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// Select mode on the Stories backlog at 900px and below (SHIP-716 D3): ⋯ More › Select, the select bar under the toolbar
// and the bulk bar fixed to the bottom with its sheets.
public sealed class SelectMode(IPage page)
{
    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SelectBar => page.GetByTestId(BacklogTestIds.SelectBar);

    public ILocator SelectBarCount => page.GetByTestId(BacklogTestIds.SelectBarCount);

    public ILocator DoneButton => page.GetByTestId(BacklogTestIds.SelectBarDone);

    public ILocator Boxes => page.GetByTestId(BacklogTestIds.TouchRowSelect);

    public ILocator Bar => page.GetByTestId(BacklogTestIds.BulkBar);

    public ILocator BarCount => page.GetByTestId(BacklogTestIds.BulkCount);

    public ILocator SprintButton => Bar.GetByTestId(BacklogTestIds.BulkTouchSprint);

    // The sprint's row in the Sprint sheet, by its name.
    public ILocator SprintOption(string name) =>
        Sheet.GetByTestId(BacklogTestIds.BulkMoveSprint).Filter(new() { HasTextString = name });

    // The select box of the row with this key.
    public ILocator Box(string key) =>
        page.GetByTestId(BacklogTestIds.Row)
            .Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^{key}$") }) })
            .GetByTestId(BacklogTestIds.TouchRowSelect);

    // ⋯ More › Select, from the toolbar's own ⋯.
    public async Task StartAsync()
    {
        await page.GetByTestId(BacklogTestIds.ToolbarRow).GetByTestId(TouchTestIds.MoreButton).TapAsync();
        await Assertions.Expect(Sheet).ToBeVisibleAsync();
        await Sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasTextString = "Select" }).First.TapAsync();
        await Assertions.Expect(SelectBar).ToBeVisibleAsync();
    }
}
