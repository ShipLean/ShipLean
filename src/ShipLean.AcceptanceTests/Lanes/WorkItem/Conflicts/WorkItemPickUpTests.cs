using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;

[TestFixture]
public class WorkItemPickUpTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenUnassignedMovedToActive_ShouldAssignMe()
    {
        var screen = new WorkItemScreen(Page);
        var fields = new FieldsStrip(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        (await WorkItemWrites.GetAsync(Page, "SHIP-147")).GetProperty("assignee").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

        await fields.PickStateAsync("Active");
        var saved = await screen.SaveAsync(SeedIds.WorkItems.Ship147);

        saved.Ok.ShouldBeTrue();
        await Expect(screen.Toast).ToContainTextAsync("Assigned to you");
        await Expect(screen.Save).ToBeDisabledAsync();
        await Expect(fields.AssigneeButton).ToContainTextAsync("Joel");
        var item = await WorkItemWrites.GetAsync(Page, "SHIP-147");
        item.GetProperty("assignee").GetProperty("id").GetGuid().ShouldBe(SeedIds.Users.Joel);
        item.GetProperty("state").GetProperty("name").GetString().ShouldBe("Active");
    }
}
