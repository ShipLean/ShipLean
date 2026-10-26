using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// What the Stories list offers beyond reading it: the filter, the drag handles, the toasts and the conflict banner.
public sealed class BacklogInteractions(IPage page)
{
    public ILocator FilterButton => page.GetByTestId(BacklogTestIds.FilterButton);

    public ILocator FilterPanel => page.GetByTestId(BacklogTestIds.FilterPanel);

    public ILocator DragOffBanner => page.GetByTestId(BacklogTestIds.DragOffBanner);

    public ILocator Grips => page.GetByTestId(BacklogTestIds.Grip);

    public ILocator Rows => page.GetByTestId(BacklogTestIds.Row);

    public ILocator Toasts => page.GetByTestId(ComponentTestIds.ToastHost).GetByTestId(ComponentTestIds.Toast);

    public ILocator ConflictBanner => page.GetByTestId(ComponentTestIds.ConflictBanner);

    public ILocator UseMine => page.GetByTestId(ComponentTestIds.ConflictUseMine);

    public ILocator KeepTheirs => page.GetByTestId(ComponentTestIds.ConflictKeepTheirs);

    // The row of an item, by its key.
    public ILocator Row(string itemKey) =>
        Rows.Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new Regex($"^{itemKey}$") }) });

    public async Task<List<string>> KeysAsync()
    {
        var keys = await page.GetByTestId(BacklogTestIds.Key).AllTextContentsAsync();
        return keys.ToList();
    }

    // Item key -> the rank the list shows for it.
    public async Task<Dictionary<string, string>> RanksAsync()
    {
        var keys = await KeysAsync();
        var ranks = await page.GetByTestId(BacklogTestIds.Rank).AllTextContentsAsync();
        return keys.Zip(ranks).ToDictionary(pair => pair.First, pair => pair.Second);
    }

    // Picks the row at `from` up by its grip and puts it down on the row at `to` (zero-based), then returns the reorder response.
    public async Task<IResponse> DragAsync(int from, int to)
    {
        var grip = (await Grips.Nth(from).BoundingBoxAsync())!;
        var target = (await Rows.Nth(to).BoundingBoxAsync())!;
        var startX = grip.X + (grip.Width / 2);
        var startY = grip.Y + (grip.Height / 2);
        return await page.RunAndWaitForResponseAsync(
            async () =>
            {
                await page.Mouse.MoveAsync(startX, startY);
                await page.Mouse.DownAsync();
                await page.Mouse.MoveAsync(startX + 5, startY + (from < to ? 20 : -20), new() { Steps = 4 });
                await page.Mouse.MoveAsync(startX + 5, target.Y + (target.Height / 2) + (from < to ? 6 : -6), new() { Steps = 12 });
                await page.Mouse.UpAsync();
            },
            r => r.Request.Method == "POST" && r.Url.EndsWith("/reorder", StringComparison.Ordinal));
    }
}
