using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.TestSupport;
using ShipLean.WebApi.Realtime;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Realtime;

[TestFixture]
public class HubSessionEndedTests : SeededAcceptanceTest
{
    [Test]
    public async Task Hub_WhenSessionEndsAndSocketDrops_ShouldSendToSignIn()
    {
        var frame = new ShellFrame(Page);
        await frame.OpenAsync(ShellFrame.BacklogPath("SHIP"));
        await new LiveStatus(Page).WaitUntilLiveAsync();

        var browser = Page.Context.Browser!;
        await using var other = await browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });
        var headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "second-context" };
        var login = await other.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = "joel@shiplean.local", password = SeedIds.Passwords.Joel },
            Headers = headers,
        });
        login.Ok.ShouldBeTrue();
        var signOut = await other.APIRequest.PostAsync("/api/auth/sign-out-others", new APIRequestContextOptions { Headers = headers });
        signOut.Status.ShouldBe(204);

        foreach (var connection in Host.Services.GetRequiredService<HubConnections>().Snapshot())
        {
            connection.Abort();
        }

        // The hub's negotiate answers session_ended and leaves the cookie, so the API call that confirms the loss answers
        // session_ended too (and deletes it): the page lands on the sign-in page with the ended note and the path to come back to.
        await Expect(Page).ToHaveURLAsync(new Regex(@"/login\?state=ended&returnUrl=%2Fp%2FSHIP%2Fbacklogs%2Fstories$"));
    }
}
