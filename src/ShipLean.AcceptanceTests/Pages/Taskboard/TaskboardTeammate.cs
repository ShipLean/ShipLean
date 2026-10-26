using System.Text.Json;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Pages.Taskboard;

// Tom, signed in on a context of his own, moving cards on the Taskboard behind the page's back.
public sealed class TaskboardTeammate(IBrowserContext context) : IAsyncDisposable
{
    private readonly IAPIRequestContext request = context.APIRequest;

    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "teammate-context" };

    public static async Task<TaskboardTeammate> SignInAsTomAsync(IPage page, string baseUrl)
    {
        var context = await page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = Headers,
        });
        login.Ok.ShouldBeTrue();
        return new TaskboardTeammate(context);
    }

    // Moves the task to another state of the same row, against the revision he reads now.
    public async Task MoveToStateAsync(Guid sprintId, string taskKey, Guid stateId)
    {
        var read = await request.GetAsync($"/api/sprints/{sprintId}/taskboard");
        read.Ok.ShouldBeTrue();
        using var view = JsonDocument.Parse(await read.TextAsync());
        var card = view.RootElement.GetProperty("rows").EnumerateArray()
            .SelectMany(row => row.GetProperty("cells").EnumerateArray())
            .SelectMany(cell => cell.GetProperty("tasks").EnumerateArray())
            .First(task => task.GetProperty("key").GetString() == taskKey);
        var moved = await request.PostAsync($"/api/sprints/{sprintId}/taskboard/moves", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new
            {
                taskId = card.GetProperty("id").GetGuid(),
                expectedRevision = card.GetProperty("revision").GetInt32(),
                stateId,
                row = (object?)null,
                beforeItemId = (Guid?)null,
            },
        });
        moved.Ok.ShouldBeTrue(await moved.TextAsync());
    }

    public async ValueTask DisposeAsync() => await context.CloseAsync();
}
