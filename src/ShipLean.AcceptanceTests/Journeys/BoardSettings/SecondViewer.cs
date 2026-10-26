using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Journeys.BoardSettings;

// Tom, signed in on a browser context of his own with a real page, so a screen he has open can be watched while Joel
// works somewhere else.
public sealed class SecondViewer(IBrowserContext context, IPage page) : IAsyncDisposable
{
    public IPage Page => page;

    public static async Task<SecondViewer> SignInAsTomAsync(IPage joelsPage, string baseUrl)
    {
        var context = await joelsPage.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "teammate-context" },
        });
        login.Ok.ShouldBeTrue();
        return new SecondViewer(context, await context.NewPageAsync());
    }

    public async ValueTask DisposeAsync() => await context.CloseAsync();
}
