using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Journeys.Collaboration;

// Tom, signed in on a browser context of his own with a real page, so what he does through the screens can be watched
// from Joel's page.
public sealed class PresenceViewer(IBrowserContext context, IPage page) : IAsyncDisposable
{
    public IPage Page => page;

    public static async Task<PresenceViewer> SignInAsTomAsync(IPage joelsPage, string baseUrl)
    {
        var context = await joelsPage.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "teammate-context" },
        });
        login.Ok.ShouldBeTrue();
        return new PresenceViewer(context, await context.NewPageAsync());
    }

    public async ValueTask DisposeAsync() => await context.CloseAsync();
}
