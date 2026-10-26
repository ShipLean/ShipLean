using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;

// The filter on the seeded Storefront backlog; it only reads.
[TestFixture]
public class ListFilterTests : SeededAcceptanceTest
{
    [Test]
    public async Task Backlog_WhenFilterOn_ShouldDisableDragWithBanner()
    {
        var backlog = new StoriesBacklog(Page);
        var list = new BacklogInteractions(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await Expect(list.Grips.First).ToBeEnabledAsync();
        await Expect(list.DragOffBanner).ToHaveCountAsync(0);
        var allRanks = await list.RanksAsync();
        var unfilteredCount = allRanks.Count;

        await list.FilterButton.ClickAsync();

        await Expect(list.FilterPanel).ToBeVisibleAsync();
        await Expect(list.DragOffBanner).ToBeVisibleAsync();
        await Expect(list.Grips.First).ToBeDisabledAsync();
        // The first clause (a type) narrows the list, and each row keeps its place in the whole backlog.
        await Expect(backlog.Rows).Not.ToHaveCountAsync(unfilteredCount);
        var filtered = await list.RanksAsync();
        filtered.Count.ShouldBeLessThan(unfilteredCount);
        foreach (var (key, rank) in filtered)
        {
            rank.ShouldBe(allRanks[key]);
        }
    }
}
