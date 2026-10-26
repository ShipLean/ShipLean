using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Auth;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.SignIn;

// Ravi signs in with the one-time password an admin gave him and must choose his own before anything opens.
[TestFixture]
public class OneTimePasswordTests : SeededAcceptanceTest
{
    private const string Email = "ravi@shiplean.local";

    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    [Order(1)]
    public async Task Login_WhenOneTimePassword_ShouldForceNewPasswordFirst()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync("/p/PAY/backlogs/stories");

        await login.SignInAsync(Email, SeedIds.Passwords.Ravi);

        await Expect(login.ChangeForm).ToBeVisibleAsync();
        await Expect(login.ChangeSubmit).ToBeDisabledAsync();
        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToHaveCountAsync(0);
    }

    [Test]
    [Order(2)]
    public async Task Login_WhenChangeStateOpenedWithoutThePassword_ShouldSignInFirst()
    {
        // Signed in on the one-time password: any page sends the browser back to the change state.
        var login = new LoginScreen(Page);
        await login.OpenAsync();
        await login.SignInAsync(Email, SeedIds.Passwords.Ravi);
        await Expect(login.ChangeForm).ToBeVisibleAsync();
        await Page.GotoAsync(Links.Home());

        await Expect(Page).ToHaveURLAsync(new Regex("/login\\?state=change&returnUrl="));
        await Expect(login.SignInForm).ToBeVisibleAsync();
        await Expect(login.Note).ToBeVisibleAsync();
    }

    [Test]
    [Order(3)]
    public async Task Login_WhenNewPasswordChosen_ShouldOpenTheReturnUrl()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync();
        await login.SignInAsync(Email, SeedIds.Passwords.Ravi);
        await Expect(login.ChangeForm).ToBeVisibleAsync();

        await login.ChooseNewPasswordAsync("a-password-only-ravi-knows");

        await Expect(Page.GetByTestId(AppTestIds.ShellTopBar)).ToContainTextAsync("PAY");
        var me = await Page.Context.APIRequest.GetAsync("/api/me");
        me.Status.ShouldBe(200);
    }
}
