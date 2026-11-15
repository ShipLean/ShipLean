using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Backlog.Touch;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Touch;

// The Stories backlog's toolbar on a phone (SHIP-716 D4, D7, D10), on the seeded SHIP backlog. Neither test changes data.
[TestFixture]
public class BacklogToolbarTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Filter_WhenClauseAddedOnPhone_ShouldApplyAndShowRemovableChip()
    {
        var backlog = new StoriesBacklog(Page);
        var toolbar = new BacklogToolbarTouch(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var all = await backlog.Rows.CountAsync();
        (await TouchRules.ViolationsAsync(Page, toolbar.Row)).ShouldBeEmpty();

        // Opening the filter starts it with a first clause (a type), which applies at once: there is no Apply button.
        await toolbar.OpenFilterAsync();
        await Expect(toolbar.AddClause).ToBeVisibleAsync();

        await Expect(backlog.Rows).Not.ToHaveCountAsync(all);
        await Expect(toolbar.FilterButton).ToHaveTextAsync("Filter (1)");
        (await TouchRules.ViolationsAsync(Page, toolbar.Sheet)).ShouldBeEmpty();
        await toolbar.CloseSheetAsync();
        await Expect(toolbar.Chip).ToHaveCountAsync(1);
        (await TouchRules.ViolationsAsync(Page, toolbar.Chips)).ShouldBeEmpty();
        var chip = await toolbar.Chip.BoundingBoxAsync();
        var row = await toolbar.Row.BoundingBoxAsync();
        chip!.Y.ShouldBeGreaterThanOrEqualTo(row!.Y + row.Height - 1);

        await toolbar.Chip.TapAsync();
        await Expect(toolbar.Chip).ToHaveCountAsync(0);
        await Expect(backlog.Rows).ToHaveCountAsync(all);
        await Expect(toolbar.FilterButton).ToHaveTextAsync("Filter");
    }

    [Test]
    public async Task Search_WhenChosenFromMoreOnPhone_ShouldNarrowRows()
    {
        var backlog = new StoriesBacklog(Page);
        var toolbar = new BacklogToolbarTouch(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var all = await backlog.Rows.CountAsync();
        var key = (await backlog.RowKeys.Nth(1).InnerTextAsync()).Trim();
        await Expect(toolbar.SearchBox).ToHaveCountAsync(0);

        await toolbar.OpenMoreAsync();
        (await TouchRules.ViolationsAsync(Page, toolbar.Sheet)).ShouldBeEmpty();
        await Expect(Page.GetByTestId(BacklogTestIds.ColumnsButton)).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(BacklogTestIds.KeysButton)).ToHaveCountAsync(0);
        await toolbar.MoreSearch.TapAsync();

        await Expect(toolbar.Sheet).ToHaveCountAsync(0);
        await Expect(toolbar.SearchBox).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, toolbar.SearchRow)).ShouldBeEmpty();
        await toolbar.SearchBox.FillAsync(key);
        await Expect(backlog.Rows).ToHaveCountAsync(1);
        await Expect(backlog.Rows.First.GetByTestId(BacklogTestIds.Key)).ToHaveTextAsync(key);
        all.ShouldBeGreaterThan(1);
    }
}
