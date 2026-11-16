using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.Contracts;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Account;

// A viewer whose browser runs in Asia/Bangkok, like BangkokViewerTest, but whose zone the test does not set: seeded
// users have none, so the first load stores the browser's zone, and the Profile page changes it.
[TestFixture]
public class ProfileTimeZoneTests : SeededAcceptanceTest
{
    private const string LondonId = "Europe/London";

    protected override string BrowserTimeZone => ViewerZoneTestData.BangkokId;

    private ILocator Current => Page.GetByTestId(SettingsTestIds.TimeZoneCurrent);

    private ILocator Select => Page.GetByTestId(SettingsTestIds.TimeZoneSelect);

    // Mutation it catches: the useBrowserZoneOnLoad() call removed from Shell.tsx (no PUT is sent, the zone stays null).
    [Test]
    public async Task FirstLoad_WhenUserHasNoZoneAndBrowserIsInBangkok_ShouldSaveAsiaBangkok()
    {
        // Priya, not Joel: no other test in this class gives her a zone, so she has none until this load.
        await SignInAsync(TestUser.Priya.Email!, TestUser.Priya.Password!);
        (await ZoneOfSignedInUserAsync()).ShouldBeNull();

        await Page.RunAndWaitForApiAsync("/api/me/time-zone", "PUT", () => Page.GotoAsync("/"));

        (await ZoneOfSignedInUserAsync()).ShouldBe(ViewerZoneTestData.BangkokId);
    }

    // Mutation it catches: the panel sends source "browser" (the server then keeps the zone already set).
    [Test]
    public async Task Profile_WhenAnotherZoneIsPicked_ShouldShowItsLabelAndKeepItAfterReload()
    {
        await SetZoneAsync(ViewerZoneTestData.BangkokId);
        var londonOffset = TimeZoneInfo.FindSystemTimeZoneById(LondonId).GetUtcOffset(TimeProvider.System.GetUtcNow());
        await Page.GotoAsync(Links.Profile());
        await Expect(Current).ToHaveTextAsync("Asia/Bangkok (UTC+07:00)");
        await Expect(Select).ToBeEnabledAsync();

        await Page.RunAndWaitForApiAsync("/api/me/time-zone", "PUT", () => Select.SelectOptionAsync(LondonId));

        var londonLabel = $"{LondonId} (UTC+{londonOffset:hh\\:mm})";
        await Expect(Current).ToHaveTextAsync(londonLabel);
        await Page.ReloadAsync();
        await Expect(Current).ToHaveTextAsync(londonLabel);
        await Expect(Select).ToHaveValueAsync(LondonId);
    }

    private async Task<string?> ZoneOfSignedInUserAsync()
    {
        var response = await Page.Context.APIRequest.GetAsync("/api/me", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
        var body = await response.JsonAsync();
        var zone = body!.Value.GetProperty("timeZone");
        return zone.ValueKind == JsonValueKind.Null ? null : zone.GetString();
    }

    private async Task SetZoneAsync(string zone)
    {
        var response = await Page.Context.APIRequest.PutAsync("/api/me/time-zone", new APIRequestContextOptions
        {
            DataObject = new { timeZone = zone, source = "profile" },
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
    }
}
