using System.Text.Json;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningMoves;

// What a teammate does behind the Planning page's back: Tom puts an item into a sprint, then places it, through the API.
internal static class TeammateSprintMove
{
    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "teammate-context" };

    public static async Task MoveAsync(IAPIRequestContext request, string itemKey, Guid sprintId, string? beforeItemKey)
    {
        var item = await BacklogWrites.GetItemAsync(request, itemKey);
        var id = item.GetProperty("id").GetGuid();
        var patch = await request.PatchAsync($"/api/work-items/{id}", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new
            {
                expectedRevision = item.GetProperty("revision").GetInt32(),
                changes = new
                {
                    title = (object?)null,
                    description = (object?)null,
                    acceptanceCriteria = (object?)null,
                    reproSteps = (object?)null,
                    severity = (object?)null,
                    state = (object?)null,
                    assignee = (object?)null,
                    sprint = new { value = sprintId },
                    priority = (object?)null,
                    points = (object?)null,
                    parent = (object?)null,
                    tags = (object?)null,
                    addLinks = Array.Empty<object>(),
                    removeLinkIds = Array.Empty<string>(),
                },
            },
        });
        patch.Ok.ShouldBeTrue(await patch.TextAsync());
        var written = JsonDocument.Parse(await patch.TextAsync()).RootElement;
        Guid? before = beforeItemKey is null
            ? null
            : (await BacklogWrites.GetItemAsync(request, beforeItemKey)).GetProperty("id").GetGuid();
        var reorder = await request.PostAsync($"/api/work-items/{id}/reorder", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new { expectedRevision = written.GetProperty("revision").GetInt32(), beforeItemId = before },
        });
        reorder.Ok.ShouldBeTrue(await reorder.TextAsync());
    }
}
