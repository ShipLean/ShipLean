using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Tree;

// A story dropped on an Epic: the server refuses the parent type. Its own class so the seeded tree stays as it was.
[TestFixture]
public class RefusedReparentTests : SeededAcceptanceTest
{
    [Test]
    public async Task Backlog_WhenReparentInvalidType_ShouldRefuseWithToast()
    {
        var tree = new TreeBacklog(Page);
        await tree.OpenAsync("SHIP");

        var response = await tree.DragAsync("SHIP-141", tree.Group("SHIP-90"), "PATCH", $"/api/work-items/{SeedIds.WorkItems.Ship141}");

        response.Ok.ShouldBeFalse();
        await Expect(tree.Toasts.First).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await tree.Groups.First.WaitForAsync();
        (await tree.KeysUnderAsync("SHIP-101")).ShouldContain("SHIP-141");
    }
}
