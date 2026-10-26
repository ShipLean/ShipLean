using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Side;

// Saving writes a revision; the history lists it first. Its own class: the item is saved.
[TestFixture]
public class WorkItemHistoryTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenHistoryOpened_ShouldListFieldChangesNewestFirst()
    {
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        await Expect(side.Revisions).ToHaveCountAsync(1);
        await Expect(side.Revisions.First).ToHaveAttributeAsync("data-rev", "1");
        await screen.Points.FillAsync("13");

        var saved = await screen.SaveAsync(SeedIds.WorkItems.Ship147);

        saved.Ok.ShouldBeTrue();
        await Expect(side.Revisions).ToHaveCountAsync(2);
        await Expect(side.Revisions.First).ToHaveAttributeAsync("data-rev", "2");
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionChange)).ToContainTextAsync("Story points");
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionChange)).ToContainTextAsync("13");
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionWho)).ToHaveTextAsync("Joel Dickson");
        await Expect(side.Revisions.Last).ToHaveAttributeAsync("data-rev", "1");
    }
}
