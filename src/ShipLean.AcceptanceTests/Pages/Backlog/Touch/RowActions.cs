using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// The row ⋯ and the Points sheet on the Stories backlog's touch rows (SHIP-716 D9).
public sealed class RowActions(IPage page)
{
    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator PointsSheet => page.GetByTestId(BacklogTestIds.PointsSheet);

    public ILocator PointsInput => page.GetByTestId(BacklogTestIds.PointsSheetInput);

    public ILocator PointsSave => page.GetByTestId(BacklogTestIds.PointsSheetSave);

    // The rows that have no estimate: their points show a ? chip.
    public ILocator UnestimatedRows => page.GetByTestId(BacklogTestIds.Row)
        .Filter(new() { Has = page.GetByTestId(BacklogTestIds.Points).Filter(new() { HasTextString = "?" }) });

    public ILocator Row(string itemKey) => page.GetByTestId(BacklogTestIds.Row).Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^{itemKey}$") }) });

    public async Task OpenMoreAsync(ILocator row)
    {
        await row.GetByTestId(TouchTestIds.MoreButton).TapAsync();
        await Assertions.Expect(Sheet).ToBeVisibleAsync();
    }

    public async Task PickAsync(string actionLabel) =>
        await Sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasTextString = actionLabel }).TapAsync();
}
