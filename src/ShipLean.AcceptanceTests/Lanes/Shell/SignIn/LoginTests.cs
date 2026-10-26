using System.Text.RegularExpressions;
using Microsoft.Playwright;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Auth;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.SignIn;

// Signing in as a seeded user with a password, and where that lands.
[TestFixture]
public class LoginTests : SeededAcceptanceTest
{
    // Nobody is signed in until the test does it through the page.
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    [Order(1)]
    public async Task Login_WhenWrongPassword_ShouldShowOneSentence()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync();
        await Expect(login.InstanceName).Not.ToBeEmptyAsync();

        await login.SignInAsync("joel@shiplean.local", "not-the-password");

        await Expect(login.Failure).ToBeVisibleAsync();
        var shown = (await login.Failure.InnerTextAsync()).Trim();
        shown.ShouldNotBeEmpty();
        shown.ShouldNotContain("joel");
        await Expect(login.Password).ToHaveValueAsync(string.Empty);

        // The same sentence whatever the reason: an email that does not exist gets it too.
        await login.SignInAsync("nobody@shiplean.local", "not-the-password");
        await Expect(login.Failure).ToHaveTextAsync(shown);
    }

    [Test]
    [Order(2)]
    public async Task Login_WhenValid_ShouldOpenReturnUrl()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync("/p/PAY/backlogs/stories");

        await login.SignInAsync("joel@shiplean.local", SeedIds.Passwords.Joel);

        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToContainTextAsync("PAY");
        await Expect(Page).ToHaveURLAsync(new Regex("/p/PAY/backlogs/stories$"));
    }

    [Test]
    [Order(3)]
    public async Task Login_WhenDeepLinkOpenedSignedOut_ShouldReturnThereAfterSignIn()
    {
        await SignOutAsync();
        // No sign-in page first: the server sends a page load with no session to /login and carries the path along.
        await Page.GotoAsync("/p/PAY/backlogs/stories?mode=tree");
        var login = new LoginScreen(Page);
        await login.SignInForm.WaitForAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/login\\?returnUrl="));

        await login.SignInAsync("joel@shiplean.local", SeedIds.Passwords.Joel);

        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToContainTextAsync("PAY");
        await Expect(Page).ToHaveURLAsync(new Regex("/p/PAY/backlogs/stories\\?mode=tree$"));
    }

    [Test]
    [Order(4)]
    public async Task Login_WhenReturnUrlIsExternal_ShouldOpenTheMostRecentProjectInstead()
    {
        await SignOutAsync();
        var login = new LoginScreen(Page);
        await login.OpenAsync("//example.com/p/PAY");

        await login.SignInAsync("joel@shiplean.local", SeedIds.Passwords.Joel);

        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToBeVisibleAsync();
        Page.Url.ShouldStartWith(BaseUrl);
    }

    [Test]
    [Order(5)]
    public async Task Root_WhenSignedIn_ShouldOpenMostRecentProject()
    {
        // Signed in through the API, so no project page is visited first (the shell remembers the project it shows).
        var headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "acceptance-test" };
        var signIn = await Page.Context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = "joel@shiplean.local", password = SeedIds.Passwords.Joel },
            Headers = headers,
        });
        signIn.Status.ShouldBe(200);
        var put = await Page.Context.APIRequest.PutAsync("/api/me/preferences/recentProjects", new APIRequestContextOptions
        {
            DataObject = new[] { SeedIds.Projects.Ops, SeedIds.Projects.Mob },
            Headers = headers,
        });
        put.Status.ShouldBe(204);

        await Page.GotoAsync(Links.Home());

        await Expect(Page).ToHaveURLAsync(new Regex("/p/OPS/backlogs/stories$"));
        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToContainTextAsync("OPS");
    }

    [Test]
    [Order(6)]
    public async Task Login_WhenReturnUrlResolvesToAnotherOrigin_ShouldOpenTheMostRecentProjectInstead()
    {
        await SignOutAsync();
        var login = new LoginScreen(Page);
        await login.OpenAsync("/.//evil.example/x");

        await login.SignInAsync("joel@shiplean.local", SeedIds.Passwords.Joel);

        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToBeVisibleAsync();
        Page.Url.ShouldStartWith(BaseUrl);
    }
}
