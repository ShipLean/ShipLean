using System.Text.RegularExpressions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.InstanceUsers;

// A person the Jira import created has no password. Joel sets up their sign-in and they get in with what he hands over.
[TestFixture]
public class UsersSetUpSignInTests : SeededAcceptanceTest
{
    [Test]
    public async Task Users_WhenImportedUserSetUp_ShouldLetThemSignIn()
    {
        var imported = new AppUserBuilder().WithPassword(null).WithName("Imported Ines");
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await imported.InsertAsync(connection);
        }
        var users = new InstanceUsersPage(Page);
        await users.OpenAsync();
        await Expect(users.StatusOf(imported.Email)).ToContainTextAsync("No password yet");

        await users.OpenMenuAsync(imported.Email, SettingsTestIds.UsersMenuPassword);
        await users.ConfirmAsync("POST", $"/api/admin/users/{await users.Row(imported.Email).GetAttributeAsync("data-user-id")}/one-time-password");
        var password = await users.Password.InnerTextAsync();
        await Page.GetByTestId(ComponentTestIds.OtpDone).ClickAsync();
        await Expect(users.StatusOf(imported.Email)).ToContainTextAsync("One-time password");

        await SignOutAsync();
        await SignInAsync(imported.Email, password);
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveURLAsync(new Regex("/login.*state=change"));
    }
}
