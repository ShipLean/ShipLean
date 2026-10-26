using System.Text.Json;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// The per-user lane choice on a board's toolbar.
public sealed class BoardLanes(IPage page)
{
    public ILocator Option(string label) =>
        page.GetByTestId(BoardTestIds.LanesOption).Filter(new LocatorFilterOptions { HasText = label });

    public ILocator Lanes => page.GetByTestId(BoardTestIds.Lane);

    // Chooses a lane option and waits for the preference to be stored.
    public async Task ChooseAsync(string label)
    {
        var saved = await page.RunAndWaitForResponseAsync(
            () => Option(label).ClickAsync(),
            r => r.Request.Method == "PUT" && r.Url.Contains("/api/me/preferences/view%3Aboard-lanes"));
        saved.Ok.ShouldBeTrue();
    }

    // How many lanes Tom, signed in on a context of his own, gets for the board.
    public static async Task<int> LanesSeenByTomAsync(IPage page, string baseUrl, Guid boardId)
    {
        var context = await page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        try
        {
            var headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "teammate-context" };
            var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
            {
                DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
                Headers = headers,
            });
            login.Ok.ShouldBeTrue();
            var read = await context.APIRequest.GetAsync($"/api/boards/{boardId}");
            read.Ok.ShouldBeTrue();
            using var view = JsonDocument.Parse(await read.TextAsync());
            return view.RootElement.GetProperty("lanes").GetArrayLength();
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
