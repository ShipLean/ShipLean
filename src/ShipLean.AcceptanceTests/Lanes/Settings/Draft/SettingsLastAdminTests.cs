using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Draft;

// Joel is the only admin of Storefront. The server refuses the save, so nothing changes.
[TestFixture]
public class SettingsLastAdminTests : SeededAcceptanceTest
{
    [Test]
    public async Task Settings_WhenLastAdminDemoted_ShouldRefuse()
    {
        var settings = new SettingsDraftPage(Page);
        await settings.OpenAsync("members");

        await settings.RoleOf("joel@shiplean.local").SelectOptionAsync("member");
        var response = await settings.SaveAsync();

        response.Status.ShouldBe(400);
        await Expect(settings.SaveError).ToContainTextAsync("at least one admin");
        await Expect(settings.Unsaved).ToContainTextAsync("1 unsaved change");
    }
}
