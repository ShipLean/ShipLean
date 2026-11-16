using Microsoft.Playwright;
using ShipLean.Contracts;
using ShipLean.TestSupport.Identity;

namespace ShipLean.AcceptanceTests.Harness;

// Base class for a scenario seen by a viewer in Asia/Bangkok, from the seed: the browser runs in Asia/Bangkok and the
// signed-in user's time zone is set to Asia/Bangkok after sign-in, through the real PUT /api/me/time-zone (source
// profile) on the page's own cookie session. Seeded users have no zone (UTC labels) until then. Expected labels come
// from ExpectedLabels with ViewerZoneTestData.Bangkok; ViewerZoneTestData.LateEveningUtc reads a day later here than in UTC.
public abstract class BangkokViewerTest : SeededAcceptanceTest
{
    private string? _zoneSetFor;

    protected override string BrowserTimeZone => ViewerZoneTestData.BangkokId;

    protected override async Task SignInAsync()
    {
        await base.SignInAsync();
        if (SignedInAs.Email is { } email && _zoneSetFor != email)
        {
            await SetViewerZoneAsync(ViewerZoneTestData.BangkokId);
            _zoneSetFor = email;
        }
    }

    // Sets the signed-in user's zone the way the Profile page does.
    protected async Task SetViewerZoneAsync(string zone)
    {
        var response = await Page.Context.APIRequest.PutAsync("/api/me/time-zone", new APIRequestContextOptions
        {
            DataObject = new { timeZone = zone, source = "profile" },
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
        });
        var answer = await response.TextAsync();
        response.Ok.ShouldBeTrue($"Setting the time zone to {zone} answered {response.Status}: {answer}");
    }
}
