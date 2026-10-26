using System.Globalization;
using System.Text.Json;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// What the Sprint backlog tests read and write through the signed-in page's API context.
internal static class BacklogApi
{
    public static async Task<JsonElement> ReadAsync(IPage page, string path)
    {
        var response = await page.Context.APIRequest.GetAsync(path);
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    // Points as the page prints them: 21, 7.5.
    public static string Pts(decimal points) => points.ToString("0.##", CultureInfo.InvariantCulture);

    // Sets the item's state as another client would, from the revision it reads now.
    public static async Task SetStateAsync(IPage page, string itemKey, Guid stateId, int? expectedRevision = null)
    {
        var item = await ReadAsync(page, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{itemKey}");
        var response = await page.Context.APIRequest.PatchAsync($"/api/work-items/{item.GetProperty("id").GetGuid()}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "teammate-context" },
            DataObject = new
            {
                expectedRevision = expectedRevision ?? item.GetProperty("revision").GetInt32(),
                changes = new
                {
                    title = (object?)null,
                    description = (object?)null,
                    acceptanceCriteria = (object?)null,
                    reproSteps = (object?)null,
                    severity = (object?)null,
                    state = new { value = stateId },
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
