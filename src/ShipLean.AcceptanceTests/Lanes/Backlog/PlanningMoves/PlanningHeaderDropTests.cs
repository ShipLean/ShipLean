using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningMoves;

// A row dropped on a sprint's header on the seeded Storefront project. Its own class: it moves the item.
[TestFixture]
public class PlanningHeaderDropTests : SeededAcceptanceTest
{
    [Test]
    public async Task Planning_WhenDroppedOnHeader_ShouldChangeSprintOnly()
    {
        var planning = new PlanningBacklog(Page);
        var moves = new PlanningDragging(Page);
        await moves.ShowWholePageAsync();
        await planning.OpenAsync("SHIP");
        var sprint26 = SeedIds.Sprints.Sprint26.ToString();
        var moved = (await moves.KeysInAsync("backlog"))[0];
        var reorders = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.EndsWith("/reorder", StringComparison.Ordinal))
            {
                reorders.Add(request.Url);
            }
        };

        var response = await moves.DragOntoHeaderAsync(moved, "Sprint 26");

        response.Ok.ShouldBeTrue(await response.TextAsync());
        await Expect(moves.Toasts.First).ToContainTextAsync($"{moved} → Sprint 26");
        (await moves.SectionOfAsync(moved)).ShouldBe(sprint26);

        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(moves.Row(moved)).ToBeVisibleAsync();
        (await moves.SectionOfAsync(moved)).ShouldBe(sprint26);
        reorders.ShouldBeEmpty();
    }
}
