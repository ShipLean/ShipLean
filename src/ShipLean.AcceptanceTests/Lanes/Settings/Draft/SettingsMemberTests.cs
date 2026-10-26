using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Draft;

// Priya is a member of Storefront, not an admin.
[TestFixture]
public class SettingsMemberTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Priya;

    [Test]
    public async Task Settings_WhenMember_ShouldBeReadOnlyWithBanner()
    {
        var settings = new SettingsDraftPage(Page);
        await settings.OpenAsync();

        await Expect(settings.ReadOnlyBanner).ToContainTextAsync("read-only");
        await Expect(settings.ReadOnlyBanner).ToContainTextAsync("Joel Dickson");
        await Expect(settings.Name).ToBeDisabledAsync();
        await Expect(settings.SaveButton).ToHaveCountAsync(0);
        await settings.TabMembers.ClickAsync();
        await Expect(settings.RoleOf("joel@shiplean.local")).ToBeDisabledAsync();
        await Expect(settings.MemberAddEmail).ToHaveCountAsync(0);
    }
}
