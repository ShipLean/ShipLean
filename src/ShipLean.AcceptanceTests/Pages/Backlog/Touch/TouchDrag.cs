namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// A real touch drag through the browser's own touch pipeline (CDP Input.dispatchTouchEvent), as K6's long-press drag
// expects it: touch down on a row's handle, hold past the 450ms lift, move in steps onto the target, let go. Mirrors
// touch/TouchHandle.phone.spec.tsx (touchInput, centre).
public static class TouchDrag
{
    // K6's long press: a handle lifts its row after 450ms held still (LIFT_MS in touch/longPress.ts).
    private const int LongPressLiftMs = 450;

    // Past the lift, so the move that follows drags the lifted row instead of scrolling. The lift is a timer in the page on
    // the real clock and the page shows nothing a locator could wait for before it fires, so the drag waits it out.
    private const int HoldMs = LongPressLiftMs + 150;

    private const double StepPixels = 20;

    public static async Task ByHandleAsync(IPage page, ILocator handle, ILocator target)
    {
        // The handle, then the target, scrolled into view; both are measured after the last scroll. They have to fit on one
        // screen: the drag does not scroll the page itself.
        await handle.ScrollIntoViewIfNeededAsync();
        await target.ScrollIntoViewIfNeededAsync();
        var from = await CentreAsync(handle);
        var to = await CentreAsync(target);
        var cdp = await page.Context.NewCDPSessionAsync(page);
        try
        {
            await SendAsync(cdp, "touchStart", from);
            await Task.Delay(HoldMs);
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2)) / StepPixels));
            for (var i = 1; i <= steps; i++)
            {
                await SendAsync(cdp, "touchMove", new Point(from.X + ((to.X - from.X) * i / steps), from.Y + ((to.Y - from.Y) * i / steps)));
            }

            await SendAsync(cdp, "touchEnd", null);
        }
        finally
        {
            await cdp.DetachAsync();
        }
    }

    private static async Task<Point> CentreAsync(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("The drag element has no box.");
        return new Point(Math.Round(box.X + (box.Width / 2)), Math.Round(box.Y + (box.Height / 2)));
    }

    private static Task SendAsync(ICDPSession cdp, string type, Point? point)
    {
        var points = point is null
            ? Array.Empty<object>()
            : [new { x = point.X, y = point.Y }];
        return cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = type, ["touchPoints"] = points });
    }

    private sealed record Point(double X, double Y);
}
