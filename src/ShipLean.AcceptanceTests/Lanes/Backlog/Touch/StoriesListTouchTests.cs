using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Backlog.Touch;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Touch;

// The Stories backlog's List mode on a phone (SHIP-716 D1), on the seeded SHIP backlog. Neither test changes data.
[TestFixture]
public class StoriesListTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task List_WhenOpenedOnPhone_ShouldShowTwoLineRowsAndNoTable()
    {
        var touch = new TouchRows(Page);

        await new StoriesBacklog(Page).OpenAsync("SHIP");
        await Expect(touch.Rows.First).ToBeVisibleAsync();

        (await touch.TableCountAsync()).ShouldBe(0);
        await Expect(Page.GetByTestId(BacklogTestIds.List)).ToHaveCountAsync(0);
        var first = touch.Rows.First;
        var title = await first.GetByTestId(BacklogTestIds.Title).BoundingBoxAsync();
        var meta = await first.GetByTestId(BacklogTestIds.TouchRowMeta).BoundingBoxAsync();
        meta!.Y.ShouldBeGreaterThanOrEqualTo(title!.Y + title.Height - 1);
        await Expect(first.GetByTestId(BacklogTestIds.Key)).ToBeVisibleAsync();
        (await touch.SidewaysOverflowAsync()).ShouldBe(0);
        (await TouchRules.ViolationsAsync(Page, touch.List)).ShouldBeEmpty();
    }

    [Test]
    public async Task Row_WhenTappedOnPhone_ShouldOpenItemModal()
    {
        var touch = new TouchRows(Page);
        var peek = new ItemPeekDialog(Page);
        await new StoriesBacklog(Page).OpenAsync("SHIP");
        var row = touch.Rows.Nth(1);
        var key = await row.GetAttributeAsync("data-key");

        await row.GetByTestId(BacklogTestIds.TouchRowMeta).TapAsync();

        await peek.WaitOpenAsync(key!);
        new Uri(Page.Url).Query.ShouldContain($"item={key}");
    }
}
