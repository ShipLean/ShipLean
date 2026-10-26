using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.InstanceUsers;

// Joel is the only instance admin. Neither guard changes anything, so the two tests share the seeded data.
[TestFixture]
public class UsersSelfProtectionTests : SeededAcceptanceTest
{
    private const string Joel = "joel@shiplean.local";

    [Test]
    public async Task Users_WhenDisablingSelf_ShouldRefuse()
    {
        var users = new InstanceUsersPage(Page);
        await users.OpenAsync();

        await users.ShowMenuAsync(Joel);

        await Expect(users.MenuItem(SettingsTestIds.UsersMenuDisable)).ToBeDisabledAsync();
        await Expect(users.MenuItem(SettingsTestIds.UsersMenuDisable)).ToContainTextAsync("You can't disable yourself");
        await Expect(users.StatusOf(Joel)).ToContainTextAsync("Active");
    }

    [Test]
    public async Task Users_WhenLastAdminDemoted_ShouldRefuse()
    {
        var users = new InstanceUsersPage(Page);
        await users.OpenAsync();

        await users.ShowMenuAsync(Joel);
        await Expect(users.MenuItem(SettingsTestIds.UsersMenuAdmin)).ToBeDisabledAsync();
        var refused = await Page.APIRequest.PutAsync(
            $"/api/admin/users/{SeedIds.Users.Joel}/instance-admin",
            new() { DataObject = new { isInstanceAdmin = false }, Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "acceptance-test" } });

        refused.Status.ShouldBe(400);
        await Page.ReloadAsync();
        await Expect(users.Row(Joel).GetByTestId(SettingsTestIds.UsersRole)).ToHaveTextAsync("Admin");
    }
}
