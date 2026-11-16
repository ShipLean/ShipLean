using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Account;

// Joel is SHIP's only admin and a member of PAY.
[TestFixture]
public class ProfileLeaveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Profile_WhenOnlyAdmin_ShouldSayOnlyAdminInsteadOfLeave()
    {
        var account = new ProfileAccountPage(Page);
        await account.OpenAsync();

        // The server still refuses an only admin's leave (ProjectSettingsApiTests.Leave_WhenOnlyAdmin_ShouldBeValidation).
        await Expect(account.ProjectRow("SHIP").GetByTestId(SettingsTestIds.MyProjectOnlyAdmin)).ToHaveTextAsync("Only admin");
        await Expect(account.ProjectRow("SHIP").GetByTestId(SettingsTestIds.MyProjectLeave)).ToHaveCountAsync(0);
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
