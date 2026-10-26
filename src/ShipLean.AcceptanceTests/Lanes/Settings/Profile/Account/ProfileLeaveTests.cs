using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Account;

// Joel is SHIP's only admin and a member of PAY.
[TestFixture]
public class ProfileLeaveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Profile_WhenOnlyAdminLeaves_ShouldRefuse()
    {
        var account = new ProfileAccountPage(Page);
        await account.OpenAsync();

        await account.LeaveAsync("SHIP");

        await Expect(account.LeaveConfirmError).ToHaveTextAsync("You are the only admin. Make someone else an admin before you leave.");
        await Expect(account.ProjectRow("SHIP")).ToBeVisibleAsync();
        var projects = await Page.Context.APIRequest.GetAsync("/api/projects");
        (await projects.TextAsync()).ShouldContain("\"SHIP\"");
    }

    [Test]
    public async Task Profile_WhenMemberLeaves_ShouldDropTheProject()
    {
        var account = new ProfileAccountPage(Page);
        await account.OpenAsync();

        await account.LeaveAsync("PAY");

        await Expect(account.ProjectRow("PAY")).ToHaveCountAsync(0);
        var projects = await Page.Context.APIRequest.GetAsync("/api/projects");
        (await projects.TextAsync()).ShouldNotContain("\"PAY\"");
    }
}
