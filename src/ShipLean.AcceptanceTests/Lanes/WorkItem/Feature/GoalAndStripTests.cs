using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

[TestFixture]
public class GoalAndStripTests : SeededAcceptanceTest
{
    [Test]
    public async Task Goal_WhenSaved_ShouldPersistAcrossReload()
    {
        var screen = new WorkItemScreen(Page);
        var strip = new FeatureStripPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");

        await strip.GoalEdit.ClickAsync();
        await strip.GoalInput.FillAsync("Returning customers pay in two taps.");
        var saved = await screen.SaveAsync(SeedIds.WorkItems.Ship101);
        saved.Ok.ShouldBeTrue();
        await Page.ReloadAsync();

        await Expect(strip.GoalView).ToHaveTextAsync("Returning customers pay in two taps.");
    }

    [Test]
    public async Task StoriesAndBugs_WhenChildClosed_ShouldUpdateCount()
    {
        var screen = new WorkItemScreen(Page);
        var strip = new FeatureStripPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");
        var feature = await BacklogApi.ReadAsync(Page, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-101");
        var children = feature.GetProperty("children").EnumerateArray()
            .Where(child => child.GetProperty("type").GetProperty("id").GetString() is "story" or "bug")
            .ToList();
        var closedBefore = children.Count(child => child.GetProperty("state").GetProperty("category").GetString() is "completed" or "removed");
        var open = children.First(child => child.GetProperty("state").GetProperty("category").GetString() is not ("completed" or "removed"));
        var key = open.GetProperty("key").GetString()!;
        var closedState = open.GetProperty("type").GetProperty("id").GetString() == "bug"
            ? SeedIds.States.Ship.BugClosed
            : SeedIds.States.Ship.StoryClosed;
        await Expect(strip.StripChildrenCount).ToHaveTextAsync($"{closedBefore} of {children.Count} closed");

        await BacklogApi.SetStateAsync(Page, key, closedState);

        await Expect(strip.StripChildrenCount).ToHaveTextAsync($"{closedBefore + 1} of {children.Count} closed");
        await Expect(strip.ChildrenCount).ToHaveTextAsync($"{closedBefore + 1}/{children.Count}");
    }
}
