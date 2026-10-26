using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// The seeded Storefront's current Sprint 24, signed in as Joel.
[TestFixture]
public class SprintBacklogPeekTests : SeededAcceptanceTest
{
    [Test]
    public async Task SprintBacklogPeek_WhenStoryTitleClicked_ShouldOpenStoryOverBacklog()
    {
        var backlog = new SprintBacklogPage(Page);
        await backlog.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);

        await backlog.StoryRow("SHIP-141").GetByTestId(ItemPeekTestIds.Link).ClickAsync();

        var peek = new ItemPeekDialog(Page);
        await peek.WaitOpenAsync("SHIP-141");
        await Expect(peek.Title).ToHaveValueAsync("Pay with saved card");
        await Expect(Page.GetByTestId(SprintsTestIds.Table)).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"\?item=SHIP-141$"));

        await Page.GoBackAsync();

        await peek.WaitClosedAsync();
        await Expect(Page.GetByTestId(SprintsTestIds.Table)).ToBeVisibleAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex(@"item="));
    }
}
