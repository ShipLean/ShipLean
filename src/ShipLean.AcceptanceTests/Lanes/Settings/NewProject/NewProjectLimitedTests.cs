using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Settings.NewProject;

// The instance limits project creation to admins; Priya is not one.
[TestFixture]
public class NewProjectLimitedTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Priya;

    [Test]
    public async Task NewProject_WhenCreationLimited_ShouldShowBlockedPage()
    {
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("UPDATE instance_settings SET project_creation = 'admins'", connection);
            await command.ExecuteNonQueryAsync();
        }
        var form = new NewProjectPage(Page);

        await form.OpenAsync();

        await Expect(form.Blocked).ToContainTextAsync("Only instance admins can create projects here");
        await Expect(form.Form).ToHaveCountAsync(0);
    }
}
