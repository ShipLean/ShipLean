using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Touch;

// The phone route sweep for the routes opened signed out (login).
[TestFixture]
public class TouchSignedOutRouteSweepTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    public static IEnumerable<string> SweptRoutes() =>
        TouchRoutes.All
            .Where(r => r.Value.SignedOut && !TouchExemptRoutes.All.ContainsKey(r.Key))
            .Select(r => r.Key)
            .Order(StringComparer.Ordinal);

    [TestCaseSource(nameof(SweptRoutes))]
    public async Task Route_WhenOpenedOnAPhoneSignedOut_ShouldFollowTheTouchRules(string screenId)
    {
        await TouchRoutes.CheckAsync(Page, screenId, Guid.Empty);
    }
}
