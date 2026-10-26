using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.InstanceUsers;

// Only an instance admin changes an email. The person signs in with the new address and no longer with the old one.
[TestFixture]
public class UsersEmailTests : SeededAcceptanceTest
{
    [Test]
    public async Task Users_WhenEmailChanged_ShouldUpdateSignIn()
    {
        var person = new AppUserBuilder().WithName("Eddie Mail");
        Guid id;
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            id = await person.InsertAsync(connection);
        }
        const string changed = "eddie.changed@example.test";
        var users = new InstanceUsersPage(Page);
        await users.OpenAsync();

        await users.OpenMenuAsync(person.Email, SettingsTestIds.UsersMenuEmail);
        await Page.GetByTestId(SettingsTestIds.UsersEmailInput).FillAsync(changed);
        await users.ConfirmAsync("PUT", $"/api/admin/users/{id}/email");

        await Expect(users.Row(changed)).ToBeVisibleAsync();
        await SignOutAsync();
        var old = await Page.APIRequest.PostAsync("/api/auth/login", new() { DataObject = new { email = person.Email, password = AppUserBuilder.ArrangedPassword }, Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "acceptance-test" } });
        old.Status.ShouldBe(401);
        await SignInAsync(changed, AppUserBuilder.ArrangedPassword);
    }
}
