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
        var overflow = await Page.EvaluateAsync<string[]>(
            "([lineId, badgeId]) => { const out = []; const root = document.documentElement; if (root.scrollWidth > root.clientWidth) out.push(`page ${root.scrollWidth} > ${root.clientWidth}`); const cell = document.querySelector(`[data-testid=\"${lineId}\"] td`); const cb = cell.getBoundingClientRect(); for (const el of cell.querySelectorAll('*')) { const b = el.getBoundingClientRect(); if (b.width > 0 && (b.right > cb.right + 0.5 || b.left < cb.left - 0.5)) out.push(`line child ${el.tagName} ${Math.round(b.right)} > ${Math.round(cb.right)}`); } if (cell.scrollWidth > cell.clientWidth) out.push(`line cell ${cell.scrollWidth} > ${cell.clientWidth}`); for (let n = cell.parentElement; n; n = n.parentElement) { if (n.scrollWidth > n.clientWidth + 1 && /(auto|scroll)/.test(getComputedStyle(n).overflowX)) out.push(`scrolls sideways: ${n.tagName}`); } const badge = document.querySelector(`[data-testid=\"${badgeId}\"]`).getBoundingClientRect(); if (badge.right > innerWidth || badge.left < 0) out.push('badge outside the window'); return out; }",
            new object[] { BacklogTestIds.PullLine, BacklogTestIds.DragBadge });
        overflow.ShouldBeEmpty();
        await Page.Mouse.UpAsync();
    }
}
