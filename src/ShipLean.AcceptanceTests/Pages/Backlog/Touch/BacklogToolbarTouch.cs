using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// The backlog toolbar at 900px and below (SHIP-716): the one row, ⋯ More, the Filter sheet with its chips, and the search
// field under the row.
public sealed class BacklogToolbarTouch(IPage page)
{
    public ILocator Row => page.GetByTestId(BacklogTestIds.ToolbarRow);

    public ILocator MoreButton => Row.GetByTestId(TouchTestIds.MoreButton);

    public ILocator FilterButton => Row.GetByTestId(BacklogTestIds.FilterButton);

    // The open bottom sheet (⋯ More or Filter).
    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator FilterSheet => page.GetByTestId(BacklogTestIds.FilterSheet);

    public ILocator Chips => page.GetByTestId(BacklogTestIds.FilterChips);

    public ILocator Chip => Chips.GetByTestId(BacklogTestIds.FilterChip);

    public ILocator SearchRow => page.GetByTestId(BacklogTestIds.SearchRow);

    public ILocator SearchBox => SearchRow.GetByTestId(BacklogTestIds.SearchBox);

    public ILocator MoreSearch => page.GetByTestId(BacklogTestIds.MoreSearch);

    public ILocator AddClause => FilterSheet.GetByTestId(CriteriaTestIds.Add);

    public async Task OpenMoreAsync()
    {
        await MoreButton.TapAsync();
        await Assertions.Expect(Sheet).ToBeVisibleAsync();
    }

    public async Task OpenFilterAsync()
    {
        await FilterButton.TapAsync();
        await Assertions.Expect(FilterSheet).ToBeVisibleAsync();
    }

    public async Task CloseSheetAsync()
    {
        await Sheet.GetByTestId(TouchTestIds.SheetClose).TapAsync();
        await Assertions.Expect(Sheet).ToHaveCountAsync(0);
    }
}
