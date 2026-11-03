using System.Text.Json;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// What Tom does behind the page's back through the bulk endpoint, and the seed of extra rows some journeys need.
internal static class TeammateBulk
{
    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "teammate-context" };

    // Moves the items to a sprint, each against the revision he just read; returns the response.
    public static async Task<IAPIResponse> MoveToSprintAsync(IAPIRequestContext request, Guid projectId, Guid sprintId, IEnumerable<string> itemKeys)
    {
        var items = new List<object>();
        foreach (var key in itemKeys)
        {
            var item = await BacklogWrites.GetItemAsync(request, key);
            items.Add(new
            {
                id = item.GetProperty("id").GetGuid(),
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
                placement = (object?)null,
            });
        }

        return await request.PostAsync($"/api/projects/{projectId}/backlog/bulk", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new { items },
        });
    }

    // Adds `count` stories at the top of the backlog in one K2 call; returns their keys in the order they were sent.
    public static async Task<List<string>> AddStoriesAsync(IAPIRequestContext request, Guid projectId, int count, string prefix)
    {
        var items = Enumerable.Range(1, count).Select(n => new
        {
            type = "story",
            title = $"{prefix} {n:000}",
            description = (string?)null,
            points = (int?)1,
            parentId = (Guid?)null,
            tasks = Array.Empty<object>(),
        }).ToList();
        var response = await request.PostAsync($"/api/projects/{projectId}/backlog/items", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new { items, sprintId = (Guid?)null, placement = (object?)null },
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("created").EnumerateArray()
            .Select(c => c.GetProperty("key").GetString()!).ToList();
    }
}
