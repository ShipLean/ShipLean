using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Draft;

// Joel, the only admin of Storefront, edits all three tabs and saves once. The class changes the project, so it has the
// seeded database to itself.
[TestFixture]
public class SettingsSaveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Settings_WhenSaved_ShouldApplyAllTabsAtOnce()
    {
        var settings = new SettingsDraftPage(Page);
        await settings.OpenAsync();

        await settings.Name.FillAsync("Storefront Plus");
        await settings.WorkingDay(6).ClickAsync();
        await settings.TabMembers.ClickAsync();
        await settings.AddMemberAsync("lee@shiplean.local");
        await Expect(settings.MemberRows).ToHaveCountAsync(7);
        await settings.RoleOf("lee@shiplean.local").SelectOptionAsync("admin");
        await settings.TabSprints.ClickAsync();
        await settings.VelocityWindow.FillAsync("4");
        await Expect(settings.SprintNaming).ToHaveValueAsync("number");
        await settings.SprintNaming.SelectOptionAsync("yearQuarterNumber");
        await Expect(settings.Unsaved).ToContainTextAsync("5 unsaved changes");

        var response = await settings.SaveAsync();
        response.Ok.ShouldBeTrue();
        await Expect(settings.Unsaved).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(settings.VelocityWindow).ToHaveValueAsync("4");
        await Expect(settings.SprintNaming).ToHaveValueAsync("yearQuarterNumber");
        await settings.TabGeneral.ClickAsync();
        await Expect(settings.Name).ToHaveValueAsync("Storefront Plus");
        await Expect(settings.WorkingDay(6)).ToHaveAttributeAsync("aria-pressed", "true");
        await settings.TabMembers.ClickAsync();
        await Expect(settings.MemberRows).ToHaveCountAsync(7);
        await Expect(settings.RoleOf("lee@shiplean.local")).ToHaveValueAsync("admin");
    }
}
