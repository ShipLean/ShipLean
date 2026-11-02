using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Backlog.BacklogApi;

namespace ShipLean.AcceptanceTests.Lanes.ItemPeek;

// The work item modal over the Stories backlog on the seeded Storefront project, signed in as Joel. The save test
// renames SHIP-141, so nothing else in this class reads its title.
[TestFixture]
public class ItemPeekBacklogTests : SeededAcceptanceTest
{
    private const string ItemKey = "SHIP-141";

    [Test]
    public async Task ItemPeek_WhenTitleClickedOnBacklog_ShouldOpenModal()
    {
        var backlog = new StoriesBacklog(Page);
        var peek = new ItemPeekDialog(Page);
        await backlog.OpenAsync("SHIP");
        var row = backlog.Rows.Filter(new() { HasText = ItemKey });

        await row.GetByTestId(BacklogTestIds.Title).ClickAsync();

        await peek.WaitOpenAsync(ItemKey);
        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(Links.Backlog("SHIP"))}\\?item={ItemKey}$"));
        await Expect(backlog.List).ToBeAttachedAsync();

        await peek.Close.ClickAsync();

        await peek.WaitClosedAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex("item="));
        await Expect(row).ToBeVisibleAsync();
    }

    [Test]
    public async Task ItemPeek_WhenSavedOnBacklog_ShouldUpdateRow()
    {
        var backlog = new StoriesBacklog(Page);
        var peek = new ItemPeekDialog(Page);
        await backlog.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var item = await ReadAsync(Page, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{ItemKey}");
        var row = backlog.Rows.Filter(new() { HasText = ItemKey });
        await row.GetByTestId(BacklogTestIds.Title).ClickAsync();
        await peek.WaitOpenAsync(ItemKey);

        await peek.Title.FillAsync("Pay with a saved card");
        var response = await Page.RunAndWaitForApiAsync(
            $"/api/work-items/{item.GetProperty("id").GetGuid()}",
            "PATCH",
            () => peek.Dialog.GetByTestId(WorkItemTestIds.Save).ClickAsync());

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var saved = await ReadAsync(Page, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{ItemKey}");
        saved.GetProperty("title").GetString().ShouldBe("Pay with a saved card");
        await peek.Close.ClickAsync();
        await peek.WaitClosedAsync();
        await Expect(row.GetByTestId(BacklogTestIds.Title)).ToHaveTextAsync("Pay with a saved card");
    }
}
