using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;

// Drag reorder on the seeded Storefront backlog. Its own class: the test changes the order.
[TestFixture]
public class ListDragTests : SeededAcceptanceTest
{
    [Test]
    public async Task Backlog_WhenRowDragged_ShouldPersistOrderAfterReload()
    {
        var backlog = new StoriesBacklog(Page);
        var list = new BacklogInteractions(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var before = (await list.KeysAsync()).Take(4).ToList();

        var response = await list.DragAsync(from: 0, to: 2);

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var expected = new[] { before[1], before[2], before[0], before[3] };
        await Expect(backlog.RowKeys.Nth(2)).ToHaveTextAsync(before[0]);

        await Page.ReloadAsync();
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        (await list.KeysAsync()).Take(4).ToArray().ShouldBe(expected);
    }
}
