using System.Text.RegularExpressions;
using Microsoft.Playwright;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Auth;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.SignIn;

// Joel is signed in by the fixture; a second signed-in context ends every other session.
[TestFixture]
public class SessionEndedTests : SeededAcceptanceTest
{
    [Test]
    public async Task Login_WhenSessionEndedAndPageReloaded_ShouldShowNote()
    {
        await Page.GotoAsync("/p/PAY/sprints?x=1");
        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToBeVisibleAsync();

        var browser = Page.Context.Browser!;
        var other = await browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });
        try
        {
            var headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "acceptance-test-other" };
            var login = await other.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
            {
                DataObject = new { email = "joel@shiplean.local", password = SeedIds.Passwords.Joel },
                Headers = headers,
            });
            login.Status.ShouldBe(200);
            var signOutOthers = await other.APIRequest.PostAsync("/api/auth/sign-out-others", new APIRequestContextOptions { Headers = headers });
            signOutOthers.Status.ShouldBe(204);
        }
        finally
        {
            await other.CloseAsync();
        }

        // A plain page load with the dead cookie: the server names the state and carries the path.
        await Page.GotoAsync("/p/PAY/sprints?x=1");

        var screen = new LoginScreen(Page);
        await Expect(Page).ToHaveURLAsync(new Regex("/login\\?state=ended&returnUrl=%2Fp%2FPAY%2Fsprints%3Fx%3D1$"));
        await Expect(screen.Note).ToContainTextAsync("signed out");
        await Expect(screen.SignInForm).ToBeVisibleAsync();
    }
}
