using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Tree;

// A story dropped on a Feature row. Its own class: the drop changes the story's parent.
[TestFixture]
public class StoryOnFeatureTests : SeededAcceptanceTest
{
    [Test]
    public async Task Backlog_WhenStoryDroppedOnFeature_ShouldReparent()
    {
        var tree = new TreeBacklog(Page);
        await tree.OpenAsync("SHIP");
        (await tree.KeysUnderAsync("SHIP-101")).ShouldContain("SHIP-141");

        var response = await tree.DragAsync("SHIP-141", tree.Group("SHIP-102"), "PATCH", $"/api/work-items/{SeedIds.WorkItems.Ship141}");

        response.Ok.ShouldBeTrue(await response.TextAsync());
        await Expect(tree.Toasts.First).ToContainTextAsync("SHIP-141 moved under Mobile sign-in");
        await Page.ReloadAsync();
        await tree.Groups.First.WaitForAsync();
        (await tree.KeysUnderAsync("SHIP-102")).ShouldContain("SHIP-141");
        (await tree.KeysUnderAsync("SHIP-101")).ShouldNotContain("SHIP-141");
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        var item = await BacklogWrites.GetItemAsync(tom, "SHIP-141");
        item.GetProperty("parent").GetProperty("key").GetString().ShouldBe("SHIP-102");
    }
}
