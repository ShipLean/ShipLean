using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Auth;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Shell.SignIn;

// Kai is an active user in no project: signing in lands on the first-run state.
[TestFixture]
public class NoProjectsTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    public async Task Login_WhenNoProjects_ShouldOfferNewProjectAndImport()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync();

        await login.SignInAsync("kai@shiplean.local", SeedIds.Passwords.Kai);

        await Expect(Page).ToHaveURLAsync(new Regex("/login\\?state=no-projects$"));
        await Expect(login.NoProjects).ToContainTextAsync("You're not in any projects yet");
        await Expect(login.NewProject).ToBeVisibleAsync();
        await Expect(login.ImportFromJira).ToBeVisibleAsync();
    }
}
