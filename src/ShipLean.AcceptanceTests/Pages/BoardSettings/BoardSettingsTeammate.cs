using System.Text.Json.Nodes;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Pages.BoardSettings;

// Tom, signed in on a context of his own, saving one section of a board's settings behind the page's back.
public sealed class BoardSettingsTeammate(IBrowserContext context) : IAsyncDisposable
{
    private readonly IAPIRequestContext request = context.APIRequest;

    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "teammate-context" };

    public static async Task<BoardSettingsTeammate> SignInAsTomAsync(IPage page, string baseUrl)
    {
        var context = await page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = Headers,
        });
        login.Ok.ShouldBeTrue();
        return new BoardSettingsTeammate(context);
    }

    // Saves the one section with `value`, based on the document he reads now.
    public async Task SaveSectionAsync(Guid boardId, string section, JsonNode value)
    {
        var settings = await ReadSettingsAsync(boardId);
        var edited = settings.DeepClone();
        edited[section] = value;
        var saved = await request.PutAsync($"/api/boards/{boardId}/settings", new APIRequestContextOptions
        {
            Headers = Headers,
            DataObject = new { basedOn = settings, settings = edited },
        });
        saved.Ok.ShouldBeTrue(await saved.TextAsync());
    }

    public async Task<JsonNode> ReadSettingsAsync(Guid boardId)
    {
        var read = await request.GetAsync($"/api/boards/{boardId}/settings");
        read.Ok.ShouldBeTrue();
        return JsonNode.Parse(await read.TextAsync())!["settings"]!;
    }

    public async ValueTask DisposeAsync() => await context.CloseAsync();
}
