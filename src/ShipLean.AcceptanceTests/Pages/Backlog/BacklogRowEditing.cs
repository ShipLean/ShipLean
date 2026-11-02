using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// Working on a Stories row where it is: its points cell and field, its title link, its checkbox and the bulk bar.
public sealed class BacklogRowEditing(IPage page)
{
    public ILocator Rows => page.GetByTestId(BacklogTestIds.Row);

    // The open points field. One is open at a time.
    public ILocator PointsField => page.GetByTestId(BacklogTestIds.PointsInput);

    public ILocator BulkBar => page.GetByTestId(BacklogTestIds.BulkBar);

    // A move to a sprint carries the row's revision (K1), unlike Top and Bottom, which are position only.
    public ILocator BulkMoveSprint => page.GetByTestId(BacklogTestIds.BulkMoveSprint);

    public ILocator BulkMoveTop => page.GetByTestId(BacklogTestIds.BulkMoveTop);

    public ILocator BulkChanged => page.GetByTestId(BacklogTestIds.BulkChanged);

    public ILocator Dialog => page.GetByTestId(ItemPeekTestIds.Dialog);

    public ILocator Row(string itemKey) =>
        Rows.Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new Regex($"^{itemKey}$") }) });

    public ILocator PointsCell(string itemKey) => Row(itemKey).GetByTestId(BacklogTestIds.Points);

    public ILocator Title(string itemKey) => Row(itemKey).GetByTestId(BacklogTestIds.Title);

    // Ctrl-click on the key cell toggles the row's selection (the checkbox has no test id of its own).
    public Task ToggleSelectAsync(string itemKey) =>
        KeyCell(itemKey).ClickAsync(new() { Modifiers = [KeyboardModifier.Control] });

    // The key cell is plain text: clicking it moves focus to the row and nothing else.
    public ILocator KeyCell(string itemKey) => Row(itemKey).GetByTestId(BacklogTestIds.Key);

    public async Task<List<string>> KeysAsync() =>
        (await page.GetByTestId(BacklogTestIds.Key).AllTextContentsAsync()).ToList();

    // True when the page, or the list's own scroller, is wider than what shows it.
    public async Task<bool> OverflowsSidewaysAsync() =>
        await page.EvaluateAsync<bool>(
            """
            () => {
              const wider = (el) => el.scrollWidth > el.clientWidth + 1;
              const list = document.querySelector('[data-testid="backlog-list"]');
              return wider(document.documentElement) || (list !== null && wider(list));
            }
            """);

    // True when the element's box sits inside the viewport sideways.
    public async Task<bool> FitsViewportAsync(ILocator element)
    {
        var box = await element.BoundingBoxAsync();
        var width = page.ViewportSize!.Width;
        return box is not null && box.X >= 0 && box.X + box.Width <= width;
    }

    // Types the number into the open field and presses Enter; returns the PATCH response that saved it.
    public Task<IResponse> TypeAndEnterAsync(string value) =>
        page.RunAndWaitForResponseAsync(
            async () =>
            {
                await PointsField.FillAsync(value);
                await PointsField.PressAsync("Enter");
            },
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));
}
