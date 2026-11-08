using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.List;

// Delete sprint, under the Edit sprint rail opened from an All sprints row, on the seeded Storefront project (SHIP) signed
// in as Joel. Only a sprint with no work items goes; Sprint 24 (the current sprint) has work items.
[TestFixture]
public class DeleteSprintTests : SeededAcceptanceTest
{
    [Test]
    public async Task Sprints_WhenAnEmptySprintIsDeleted_ShouldDropItsRow()
    {
        var empty = await SprintDatabase.InsertEmptySprintAsync(ConnectionString, "SHIP", "Sprint 90", new DateOnly(2090, 1, 2), new DateOnly(2090, 1, 15));
        var sprints = new AllSprints(Page);
        await sprints.OpenAsync("SHIP");

        await sprints.EditButtonOf("Sprint 90").ClickAsync();
        await sprints.DeleteButton.ClickAsync();
        var deleted = await sprints.ConfirmDeleteAsync(empty);

        deleted.Status.ShouldBe(204);
        await Expect(sprints.RowOf("Sprint 90")).ToHaveCountAsync(0);
        await Expect(sprints.EditRail).ToHaveCountAsync(0);
        (await SprintDatabase.IsArchivedAsync(ConnectionString, empty)).ShouldBeTrue();
    }

    [Test]
    public async Task Sprints_WhenASprintWithWorkItemsIsDeleted_ShouldSayWhyAndKeepIt()
    {
        var sprints = new AllSprints(Page);
        await sprints.OpenAsync("SHIP");

        await sprints.EditButtonOf("Sprint 24").ClickAsync();
        await sprints.DeleteButton.ClickAsync();
        var refused = await sprints.ConfirmDeleteAsync(SeedIds.Sprints.Sprint24);

        refused.Status.ShouldBe(409);
        await Expect(sprints.DeleteError).ToHaveTextAsync("Sprint 24 has work items, so it can't be deleted. Move them to another sprint or the backlog first.");
        await Expect(sprints.RowOf("Sprint 24")).ToHaveCountAsync(1);
        (await SprintDatabase.IsArchivedAsync(ConnectionString, SeedIds.Sprints.Sprint24)).ShouldBeFalse();
    }
}
