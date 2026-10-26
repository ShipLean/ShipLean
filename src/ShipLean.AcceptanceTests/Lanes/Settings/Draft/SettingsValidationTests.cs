using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Draft;

// Refusals that save nothing, so the tests share the seeded database.
[TestFixture]
public class SettingsValidationTests : SeededAcceptanceTest
{
    [Test]
    public async Task Settings_WhenNoWorkingDay_ShouldJumpToError()
    {
        var settings = new SettingsDraftPage(Page);
        await settings.OpenAsync();
        for (var day = 1; day <= 5; day++)
        {
            await settings.WorkingDay(day).ClickAsync();
        }

        await settings.TabSprints.ClickAsync();
        await settings.SaveButton.ClickAsync();

        await Expect(settings.FieldError).ToContainTextAsync("at least one working day");
        await Expect(settings.WorkingDays).ToBeFocusedAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/settings/general$"));
    }

    [Test]
    public async Task Settings_WhenMemberEmailPartial_ShouldSayNotFound()
    {
        var settings = new SettingsDraftPage(Page);
        await settings.OpenAsync("members");
        await Expect(settings.MemberRows).ToHaveCountAsync(6);

        await settings.AddMemberAsync("lee@shiplean");

        await Expect(settings.MemberAddError).ToContainTextAsync("No user has that email address.");
        await Expect(settings.MemberRows).ToHaveCountAsync(6);
    }
}
