using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// Planning with a selection held in the air over a sprint and the pull line showing, at the two widths the app is used at. Nothing is dropped.
[TestFixture]
public class PullLayoutTests : SeededAcceptanceTest
{
    [TestCase(1280)]
    [TestCase(1440)]
    public async Task Planning_WhenDraggingWithPullLine_ShouldNotOverflow(int width)
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        var pull = new PullLine(Page);
        await Page.SetViewportSizeAsync(width, 900);
        await planning.OpenAsync("SHIP");
        await moves.SelectAsync("SHIP-162");
        await moves.SelectAsync("SHIP-163");
        await pull.DragGripBelowAsync(moves.Row("SHIP-160"));
        await Expect(pull.Sum).ToHaveAttributeAsync("data-over", "true");

        await moves.PickUpAndHoverAsync("SHIP-162", moves.Row("SHIP-153"));
        await Expect(moves.DragBadge).ToBeVisibleAsync();

        // Nothing sticks out of the window, the list or the pull line's own cell, with the badge up and the line showing.
        var overflow = new List<string>();
        var page = await Page.EvaluateAsync<int[]>("() => [document.documentElement.scrollWidth, document.documentElement.clientWidth]");
        if (page[0] > page[1])
        {
            overflow.Add($"page {page[0]} > {page[1]}");
        }

        var cell = (await pull.Cell.BoundingBoxAsync())!;
        foreach (var (name, part) in new[] { ("content", pull.Content), ("grip", pull.Grip), ("target", pull.Target), ("sum", pull.Sum), ("go", pull.Go) })
        {
            var b = (await part.BoundingBoxAsync())!;
            if (b.X + b.Width > cell.X + cell.Width + 0.5 || b.X < cell.X - 0.5)
            {
                overflow.Add($"line {name} {Math.Round(b.X + b.Width)} > {Math.Round(cell.X + cell.Width)}");
            }
        }

        var cellScroll = await pull.Cell.EvaluateAsync<int[]>("el => [el.scrollWidth, el.clientWidth]");
        if (cellScroll[0] > cellScroll[1])
        {
            overflow.Add($"line cell {cellScroll[0]} > {cellScroll[1]}");
        }

        var scrolling = await pull.Cell.EvaluateAsync<string[]>(
            "el => { const out = []; for (let n = el.parentElement; n; n = n.parentElement) { if (n.scrollWidth > n.clientWidth + 1 && /(auto|scroll)/.test(getComputedStyle(n).overflowX)) out.push(`scrolls sideways: ${n.tagName}`); } return out; }");
        overflow.AddRange(scrolling);

        var badge = (await moves.DragBadge.BoundingBoxAsync())!;
        if (badge.X + badge.Width > width || badge.X < 0)
        {
            overflow.Add("badge outside the window");
        }

        overflow.ShouldBeEmpty();

        // Nothing is dropped: Escape cancels the drag, and no bulk write was sent.
        var bulk = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal))
            {
                bulk.Add(request.Url);
            }
        };
        await Page.Keyboard.PressAsync("Escape");
        await Expect(moves.DragBadge).ToBeHiddenAsync();
        await Expect(moves.Row("SHIP-153")).Not.ToHaveAttributeAsync("data-over", "true");
        await Page.Mouse.UpAsync();
        await Expect(moves.DragBadge).ToBeHiddenAsync();
        bulk.ShouldBeEmpty();
    }
}
