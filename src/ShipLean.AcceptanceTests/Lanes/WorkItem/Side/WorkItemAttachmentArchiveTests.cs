using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Side;

[TestFixture]
public class WorkItemAttachmentArchiveTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenAttachmentArchived_ShouldStayInDatabase()
    {
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-150");
        (await side.UploadAsync(SeedIds.WorkItems.Ship150, "old.txt", [1, 2, 3])).Ok.ShouldBeTrue();
        await Expect(side.Attachments).ToHaveCountAsync(1);

        var response = await Page.RunAndWaitForResponseAsync(
            () => side.ArchiveButtons.First.ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/archive", StringComparison.Ordinal));

        response.Ok.ShouldBeTrue();
        await Expect(side.Attachments).ToHaveCountAsync(0);
        await Expect(side.AttachmentCount).ToHaveTextAsync("0");
        var row = await WorkItemAttachments.RowAsync(ConnectionString, SeedIds.WorkItems.Ship150, "old.txt");
        row.ShouldNotBeNull().Archived.ShouldBeTrue();
        await Page.ReloadAsync();
        await screen.Screen.WaitForAsync();
        await Expect(side.Attachments).ToHaveCountAsync(0);
    }
}
