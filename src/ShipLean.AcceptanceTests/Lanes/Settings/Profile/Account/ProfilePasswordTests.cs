using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Account;

// Joel signs in on a second browser, then changes his password in the first: the second session ends, the first stays.
[TestFixture]
public class ProfilePasswordTests : SeededAcceptanceTest
{
    [Test]
    public async Task Profile_WhenPasswordChanged_ShouldEndOtherSessions()
    {
        await using var other = await Page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });
        var login = await other.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = "joel@shiplean.local", password = SeedIds.Passwords.Joel },
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "second-context" },
        });
        login.Ok.ShouldBeTrue();
        var otherPage = await other.NewPageAsync();
        await new ProfileAccountPage(otherPage).OpenAsync();

        var account = new ProfileAccountPage(Page);
        await account.OpenAsync();
        await account.ChangePasswordAsync(SeedIds.Passwords.Joel, "a-brand-new-password");

        await Page.ReloadAsync();
        await Expect(account.Panel).ToBeVisibleAsync();
        await otherPage.ReloadAsync();
        await Expect(otherPage).ToHaveURLAsync(new Regex("/login"));
    }

    [Test]
    public async Task Profile_WhenCurrentPasswordWrong_ShouldShowTheServersMessage()
    {
        var account = new ProfileAccountPage(Page);
        await account.OpenAsync();

        await account.ChangePasswordAsync("not-my-password", "another-new-password");

        await Expect(account.PasswordError).ToHaveTextAsync("The current password is wrong.");
    }
}
