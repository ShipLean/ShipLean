using System.Text.Json;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;

// What the tests do to a work item behind the page's back: read it and write it through the API as the signed-in user.
internal static class WorkItemWrites
{
    public static async Task<JsonElement> GetAsync(IPage page, string itemKey)
    {
        var response = await page.Context.APIRequest.GetAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{itemKey}");
        response.Ok.ShouldBeTrue();
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    // A teammate's save: sets story points and priority, expecting the revision the item is at.
    public static async Task SavePointsAndPriorityAsync(IPage page, Guid itemId, int expectedRevision, int points, int priority)
    {
        var response = await page.Context.APIRequest.PatchAsync($"/api/work-items/{itemId}", new APIRequestContextOptions
        {
            DataObject = new
            {
                expectedRevision,
                changes = new
                {
                    title = (object?)null,
                    description = (object?)null,
                    acceptanceCriteria = (object?)null,
                    reproSteps = (object?)null,
                    severity = (object?)null,
                    state = (object?)null,
                    assignee = (object?)null,
                    sprint = (object?)null,
                    priority = new { value = priority },
                    points = new { value = points },
                    parent = (object?)null,
                    tags = (object?)null,
                    addLinks = Array.Empty<object>(),
                    removeLinkIds = Array.Empty<string>(),
                },
            },
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
    }
}
