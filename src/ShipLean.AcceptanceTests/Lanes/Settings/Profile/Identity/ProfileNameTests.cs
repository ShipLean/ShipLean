using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Identity;

// Joel renames himself and picks a colour. The save finishes before the reload, and the shell's own user button shows the new name.
[TestFixture]
public class ProfileNameTests : SeededAcceptanceTest
{
    [Test]
    public async Task Profile_WhenNameChanged_ShouldUpdateMe()
    {
        var profile = new ProfileIdentityPage(Page);
        await profile.OpenAsync();

        await profile.Name.FillAsync("Joel Q. Dickson");
        await profile.PurpleSwatch.ClickAsync();
        await Expect(profile.Avatar).ToHaveTextAsync("JD");
        await profile.SaveAsync("PUT", "/api/me/profile");

        await Expect(profile.ShellUserButton).ToContainTextAsync("Joel Q. Dickson");
        await Page.ReloadAsync();
        await Expect(profile.Name).ToHaveValueAsync("Joel Q. Dickson");
        await Expect(Page.GetByTestId(SettingsTestIds.ProfileHeading)).ToHaveTextAsync("Joel Q. Dickson");
    }
}
