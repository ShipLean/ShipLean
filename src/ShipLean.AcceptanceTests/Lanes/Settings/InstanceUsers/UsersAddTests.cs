using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.InstanceUsers;

// Joel, the seeded instance admin, adds a person to EMPT. The one-time password is on screen until Done, in no later read.
[TestFixture]
public class UsersAddTests : SeededAcceptanceTest
{
    [Test]
    public async Task Users_WhenAdded_ShouldShowOneTimePasswordOnce()
    {
        var users = new InstanceUsersPage(Page);
        await users.OpenAsync();

        await users.AddAsync("nora.new@example.test", "Nora New");
        await Expect(users.Password).ToBeVisibleAsync();
        var password = await users.Password.InnerTextAsync();
        await Page.GetByTestId(ComponentTestIds.OtpDone).ClickAsync();
        await Expect(users.Password).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(users.Row("nora.new@example.test")).ToBeVisibleAsync();
        await Expect(users.StatusOf("nora.new@example.test")).ToContainTextAsync("One-time password");
        await Expect(users.Row("nora.new@example.test").GetByTestId(SettingsTestIds.UsersProjects)).ToHaveTextAsync("EMPT");
        (await Page.ContentAsync()).ShouldNotContain(password);
    }
}
