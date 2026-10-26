using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Tree;

// A story dropped on a story under another Feature. Its own class: the drop changes the story's parent and place.
[TestFixture]
public class StoryOnOtherFeatureRowTests : SeededAcceptanceTest
{
    [Test]
    public async Task Backlog_WhenDroppedOnRowInOtherFeature_ShouldTakeThatParent()
    {
        var tree = new TreeBacklog(Page);
        await tree.OpenAsync("SHIP");
        (await tree.KeysUnderAsync("SHIP-102")).ShouldContain("SHIP-129");

        var response = await tree.DragAsync("SHIP-141", tree.Row("SHIP-129"), "POST", $"/api/work-items/{SeedIds.WorkItems.Ship141}/reorder");

        response.Ok.ShouldBeTrue(await response.TextAsync());
        await Page.ReloadAsync();
        await tree.Groups.First.WaitForAsync();
        var under = await tree.KeysUnderAsync("SHIP-102");
        under.ShouldContain("SHIP-141");
        under.IndexOf("SHIP-141").ShouldBe(under.IndexOf("SHIP-129") - 1);
        (await tree.KeysUnderAsync("SHIP-101")).ShouldNotContain("SHIP-141");
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        var item = await BacklogWrites.GetItemAsync(tom, "SHIP-141");
        item.GetProperty("parent").GetProperty("key").GetString().ShouldBe("SHIP-102");
    }
}
