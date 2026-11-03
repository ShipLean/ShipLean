using System.Text.Json;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningAids;

// What the tests do behind the page's back, through the API the way another tab or a teammate would.
internal static class PlanningAidsApi
{
    // A client id that is not the page's, so the page takes the change as someone else's and refetches.
    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "arrange" };

    public static async Task<JsonElement> ReadAsync(IAPIRequestContext request, string path)
    {
        var response = await request.GetAsync(path);
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    public static Task<JsonElement> ItemAsync(IAPIRequestContext request, string itemKey) =>
        ReadAsync(request, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{itemKey}");

    // Writes acceptance criteria on the item, so the "no acceptance criteria" check no longer flags it.
    public static async Task SetCriteriaAsync(IAPIRequestContext request, string itemKey, string criteria)
    {
        var item = await ItemAsync(request, itemKey);
        var response = await request.PatchAsync($"/api/work-items/{item.GetProperty("id").GetGuid()}", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new
            {
                expectedRevision = item.GetProperty("revision").GetInt32(),
                changes = new
                {
                    title = (object?)null,
                    description = (object?)null,
                    acceptanceCriteria = new { value = new[] { new { text = criteria, done = false } } },
                    reproSteps = (object?)null,
                    severity = (object?)null,
                    state = (object?)null,
                    assignee = (object?)null,
                    sprint = (object?)null,
                    priority = (object?)null,
                    points = (object?)null,
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
