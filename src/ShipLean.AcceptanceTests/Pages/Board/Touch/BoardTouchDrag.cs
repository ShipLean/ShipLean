using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board.Touch;

// A finger on the phone board (SHIP-719): Chrome's own touch input over CDP, so the browser's scrolling and cancelable
// touchmove run. The long press and the edge hold run in real time; every wait is for something on screen.
public sealed class BoardTouchDrag(IPage page)
{
    private const int EdgeInsetPx = 10;
    private ICDPSession? cdp;

    private async Task SendAsync(string type, double? x = null, double? y = null)
    {
        cdp ??= await page.Context.NewCDPSessionAsync(page);
        object[] points = x is null || y is null ? [] : [new { x = (int)x.Value, y = (int)y.Value }];
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = type, ["touchPoints"] = points });
    }

    private static (double X, double Y) CentreOf(LocatorBoundingBoxResult box) => (box.X + (box.Width / 2), box.Y + (box.Height / 2));

    public ILocator Ghost => page.GetByTestId(TouchTestIds.DragGhost);

    // The pager chip of a column by its name; the page on screen has aria-current.
    private ILocator Chip(string columnName) =>
        page.GetByTestId(TouchTestIds.PagerChip).Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^\\s*" + Regex.Escape(columnName) + "(?![A-Za-z])") });

    // Resolves once a cell of the column (in the lane, when given) lies inside the viewport: the smooth scroll has settled.
    private async Task CellInViewAsync(string columnId, string? lane) =>
        await page.WaitForFunctionAsync(
            "([lane, column]) => [...document.querySelectorAll('[data-lane][data-column]')].some(c => c.dataset.column === column"
            + " && (lane === null || c.dataset.lane === lane) && c.getBoundingClientRect().left >= 0"
            + " && c.getBoundingClientRect().right <= window.innerWidth)",
            new[] { lane, columnId });

    // Turns the pager to the column (its chip) and waits for the smooth scroll to settle, so its cards are on screen.
    public async Task ShowColumnAsync(string columnName)
    {
        await Chip(columnName).TapAsync();
        await Assertions.Expect(Chip(columnName)).ToHaveAttributeAsync("aria-current", "true");
        await CellInViewAsync((await Chip(columnName).GetAttributeAsync("data-page-id"))!, null);
    }

    // Presses the card and waits for it to lift (the ghost shows).
    public async Task LiftAsync(ILocator card)
    {
        var (x, y) = CentreOf((await card.BoundingBoxAsync())!);
        await SendAsync("touchStart", x, y);
        await Assertions.Expect(Ghost).ToBeVisibleAsync();
    }

    public async Task ReleaseAsync() => await SendAsync("touchEnd");

    // With the card lifted (LiftAsync): holds at the right edge until the column's page is on screen, then drops the card
    // in its own lane on that page; returns the moves response.
    public async Task<IResponse> DragLiftedToColumnAsync(ILocator card, ILocator cardCell, string columnName)
    {
        var lane = await cardCell.GetAttributeAsync("data-lane");
        var width = await page.EvaluateAsync<int>("window.innerWidth");
        var (_, y) = CentreOf((await card.BoundingBoxAsync())!);
        await SendAsync("touchMove", width - EdgeInsetPx, y);
        // The page turns again every 800ms while the finger stays at the edge: leave it as soon as this one is on screen.
        await Assertions.Expect(Chip(columnName)).ToHaveAttributeAsync("aria-current", "true");
        await SendAsync("touchMove", width / 2, y);
        var columnId = (await Chip(columnName).GetAttributeAsync("data-page-id"))!;
        await CellInViewAsync(columnId, lane);
        // The cell by its attributes: the lane's Doing-half cell in that column's page.
        var centre = await page.EvaluateAsync<double[]>(
            "([lane, column]) => { const r = [...document.querySelectorAll('[data-lane][data-column]')]"
            + ".find(c => c.dataset.lane === lane && c.dataset.column === column && c.dataset.doneHalf === 'false')"
            + ".getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height / 2]; }",
            new[] { lane, columnId });
        await SendAsync("touchMove", centre[0], centre[1]);
        return await page.RunAndWaitForResponseAsync(
            ReleaseAsync,
            r => r.Request.Method == "POST" && Regex.IsMatch(new Uri(r.Url).AbsolutePath, "^/api/boards/[^/]+/moves$"));
    }
}
