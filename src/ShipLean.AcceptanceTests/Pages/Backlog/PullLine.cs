using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The pull line in the Planning Backlog: where it is drawn, what it says it would do, its grip and Pull.
public sealed class PullLine(IPage page)
{
    public ILocator Line => page.GetByTestId(BacklogTestIds.PullLine);

    public ILocator Grip => page.GetByTestId(BacklogTestIds.PullGrip);

    public ILocator Target => page.GetByTestId(BacklogTestIds.PullTarget);

    public ILocator Sum => page.GetByTestId(BacklogTestIds.PullSum);

    public ILocator Reset => page.GetByTestId(BacklogTestIds.PullReset);

    public ILocator Go => page.GetByTestId(BacklogTestIds.PullGo);

    public ILocator Cell => page.GetByTestId(BacklogTestIds.PullCell);

    public ILocator Content => page.GetByTestId(BacklogTestIds.PullContent);

    // The keys of the Backlog's rows above the line, top to bottom: the rows Pull would take.
    public async Task<List<string>> KeysAboveAsync()
    {
        var line = (await Line.ElementHandleAsync());
        var keys = await page.GetByTestId(BacklogTestIds.Row).EvaluateAllAsync<string[]>(
            "(rows, line) => rows.filter(r => r.getAttribute('data-section') === 'backlog' && (line.compareDocumentPosition(r) & Node.DOCUMENT_POSITION_PRECEDING) !== 0).map(r => r.getAttribute('data-key') ?? '')",
            line);
        return [.. keys];
    }

    // Drags the grip to just below `row`, one pointer move at a time, and waits for the line to be drawn there.
    public async Task DragGripBelowAsync(ILocator row)
    {
        var grip = (await Grip.BoundingBoxAsync())!;
        var box = (await row.BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(grip.X + (grip.Width / 2), grip.Y + (grip.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(grip.X + (grip.Width / 2), box.Y + box.Height + 4, new() { Steps = 10 });
        await page.Mouse.UpAsync();
    }
}
