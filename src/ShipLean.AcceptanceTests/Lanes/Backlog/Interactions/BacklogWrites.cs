using System.Text.Json;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;

// What a teammate does behind the page's back: a second browser context signed in as Tom, writing through the API.
internal static class BacklogWrites
{
    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "teammate-context" };

    public static async Task<IAPIRequestContext> SignInAsTomAsync(IPage page, string baseUrl)
    {
        var browser = page.Context.Browser!;
        var context = await browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = Headers,
        });
        login.Ok.ShouldBeTrue();
        return context.APIRequest;
    }

    public static async Task<JsonElement> GetItemAsync(IAPIRequestContext request, string itemKey)
    {
        var response = await request.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{itemKey}");
        response.Ok.ShouldBeTrue();
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    // Tom sets the item's story points, against the revision he just read.
    public static async Task SetPointsAsync(IAPIRequestContext request, string itemKey, int points)
    {
        var item = await GetItemAsync(request, itemKey);
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
                    acceptanceCriteria = (object?)null,
                    reproSteps = (object?)null,
                    severity = (object?)null,
                    state = (object?)null,
                    assignee = (object?)null,
                    sprint = (object?)null,
                    priority = (object?)null,
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
