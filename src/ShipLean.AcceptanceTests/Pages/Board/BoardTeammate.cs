using System.Text.Json;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Pages.Board;

// Tom, signed in on a context of his own, moving cards on a board behind the page's back.
public sealed class BoardTeammate(IBrowserContext context) : IAsyncDisposable
{
    private readonly IAPIRequestContext request = context.APIRequest;

    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "teammate-context" };

    public static async Task<BoardTeammate> SignInAsTomAsync(IPage page, string baseUrl)
    {
        var context = await page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = Headers,
        });
        login.Ok.ShouldBeTrue();
        return new BoardTeammate(context);
    }

    // Moves the card into a column (and half) of the same lane, against the revision he reads now.
    public async Task MoveToColumnAsync(Guid boardId, string cardKey, string columnId, bool done = false)
    {
        var read = await request.GetAsync($"/api/boards/{boardId}");
        read.Ok.ShouldBeTrue();
        using var view = JsonDocument.Parse(await read.TextAsync());
        var card = view.RootElement.GetProperty("lanes").EnumerateArray()
            .SelectMany(lane => lane.GetProperty("cells").EnumerateArray())
            .SelectMany(cell => cell.GetProperty("cards").EnumerateArray())
            .First(c => c.GetProperty("key").GetString() == cardKey);
        var moved = await request.PostAsync($"/api/boards/{boardId}/moves", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new
            {
                itemId = card.GetProperty("id").GetGuid(),
                expectedRevision = card.GetProperty("revision").GetInt32(),
                columnId,
                done,
                laneId = (string?)null,
                beforeItemId = (Guid?)null,
            },
        });
        moved.Ok.ShouldBeTrue(await moved.TextAsync());
    }

    public async ValueTask DisposeAsync() => await context.CloseAsync();
}
