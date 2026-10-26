using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The Stories backlog in tree mode: Epic and Feature groups with their stories, dragged by the grip.
public sealed class TreeBacklog(IPage page)
{
    public ILocator Groups => page.GetByTestId(BacklogTestIds.TreeGroup);

    public ILocator Rows => page.GetByTestId(BacklogTestIds.Row);

    public ILocator Toasts => page.GetByTestId(ComponentTestIds.ToastHost).GetByTestId(ComponentTestIds.Toast);

    // The group row of an Epic or Feature, by its key.
    public ILocator Group(string key) =>
        Groups.Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = Exact(key) }) });

    // The row of a story or bug, by its key.
    public ILocator Row(string key) =>
        Rows.Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = Exact(key) }) });

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.Backlog(projectKey, mode: "tree"));
        await Groups.First.WaitForAsync();
    }

    // Every key on screen in order, group rows included.
    public async Task<List<string>> KeysAsync() =>
        (await page.GetByTestId(BacklogTestIds.Key).AllTextContentsAsync()).ToList();

    public async Task<List<string>> GroupKeysAsync() =>
        (await Groups.GetByTestId(BacklogTestIds.Key).AllTextContentsAsync()).ToList();

    // The keys of the stories shown under a group, up to the next group row.
    public async Task<List<string>> KeysUnderAsync(string groupKey)
    {
        var groups = await GroupKeysAsync();
        var keys = await KeysAsync();
        return keys.SkipWhile(k => k != groupKey).Skip(1).TakeWhile(k => !groups.Contains(k)).ToList();
    }

    // Picks a story up by its grip and puts it down on `target`, then returns the response of the last write the drop
    // sends: the PATCH for a drop on a group, the reorder POST for a drop on a story.
    public async Task<IResponse> DragAsync(string key, ILocator target, string method, string pathEnd)
    {
        var grip = (await Row(key).GetByTestId(BacklogTestIds.Grip).BoundingBoxAsync())!;
        var box = (await target.BoundingBoxAsync())!;
        var startX = grip.X + (grip.Width / 2);
        var startY = grip.Y + (grip.Height / 2);
        return await page.RunAndWaitForResponseAsync(
            async () =>
            {
                await page.Mouse.MoveAsync(startX, startY);
                await page.Mouse.DownAsync();
                await page.Mouse.MoveAsync(startX + 5, startY + 10, new() { Steps = 4 });
                await page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2), new() { Steps = 12 });
                await page.Mouse.UpAsync();
            },
            r => r.Request.Method == method && r.Url.EndsWith(pathEnd, StringComparison.Ordinal));
    }

    private static Regex Exact(string key) => new($"^{key}$");
}
