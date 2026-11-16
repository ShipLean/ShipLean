using System.Net;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.TestSupport.Identity;

// Sets a user's time zone the way the Profile page does: the real PUT /api/me/time-zone with source profile, so the
// cached user is forgotten and the user's next request reads in the new zone.
public static class ViewerZoneArrange
{
    public static async Task SetAsync(ApiClient api, TestUser user, string zone)
    {
        using var response = await api.ActingAs(user).PutJsonAsync("/api/me/time-zone", new { timeZone = zone, source = "profile" });
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Setting the time zone to {zone} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}
