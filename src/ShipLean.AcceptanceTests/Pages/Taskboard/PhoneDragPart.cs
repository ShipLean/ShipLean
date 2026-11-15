using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Taskboard;

// A finger on the shared Taskboard grid on a phone (SHIP-722): Chrome's own touch input over CDP, so the browser's scrolling
// and cancelable touchmove run. The long press and the edge hold run in real time; every wait is for something on screen.
public sealed class PhoneDragPart(IPage page)
{
    private const int EdgeInsetPx = 10;
    private ICDPSession? cdp;

    public ILocator Ghost => page.GetByTestId(TouchTestIds.DragGhost);

    private async Task SendAsync(string type, double? x = null, double? y = null)
    {
        cdp ??= await page.Context.NewCDPSessionAsync(page);
        object[] points = x is null || y is null ? [] : [new { x = (int)x.Value, y = (int)y.Value }];
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = type, ["touchPoints"] = points });
    }

    private static (double X, double Y) CentreOf(LocatorBoundingBoxResult box) => (box.X + (box.Width / 2), box.Y + (box.Height / 2));

    // Presses the card and waits for it to lift (the ghost shows). Returns where the finger is.
    public async Task<(double X, double Y)> LiftAsync(ILocator card)
    {
        await card.ScrollIntoViewIfNeededAsync();
        var at = CentreOf((await card.BoundingBoxAsync())!);
        await SendAsync("touchStart", at.X, at.Y);
        await Assertions.Expect(Ghost).ToBeVisibleAsync();
        return at;
    }

    public Task ReleaseAsync() => SendAsync("touchEnd");

    // With a card lifted: moves the finger onto the middle of the element, which must be on screen.
    public async Task MoveOverAsync(ILocator target)
    {
        await Assertions.Expect(target).ToBeInViewportAsync();
        var (x, y) = CentreOf((await target.BoundingBoxAsync())!);
        await SendAsync("touchMove", x, y);
    }

    // With a card lifted: holds at the right edge, at the finger's height; the caller moves away once the page it wants shows.
    public async Task HoldAtRightEdgeAsync(double y)
    {
        var width = await page.EvaluateAsync<int>("window.innerWidth");
        await SendAsync("touchMove", width - EdgeInsetPx, y);
    }

    // With a card lifted: moves the finger to the middle of the screen's width at the given height.
    public async Task MoveToMiddleAsync(double y)
    {
        var width = await page.EvaluateAsync<int>("window.innerWidth");
        await SendAsync("touchMove", width / 2, y);
    }
}
