using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Usage.Surface;

// The surface of a tracked action follows the principal: a request made with a device bearer is `app`, a cookie request `web`.
// Both call the same tracked action (Auth.SignOutOthers). No test sign-in, so a bearer is authenticated by DeviceBearer alone.
[TestFixture]
public class UsageSurfaceAppTests : ApiFixture
{
    private const string Path = "/api/auth/sign-out-others";

    protected override bool UseTestSignIn => false;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task ActionUsage_WhenDeviceBearer_ShouldTrackSurfaceApp()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);
        Recorder.Clear();

        using var response = await phone.Client.PostAsync(Path, null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Events.Single(e => e.Name == UsageEventNames.AuthOtherSessionsSignedOut).Surface.ShouldBe(UsageSurface.App);
    }

    [Test]
    public async Task ActionUsage_WhenCookie_ShouldTrackSurfaceWeb()
    {
        using var browser = await AuthClient.SignedInAsync(Host, "joel@shiplean.local", SeedIds.Passwords.Joel);
        Recorder.Clear();

        using var response = await browser.PostAsync(Path, null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Recorder.Events.Single(e => e.Name == UsageEventNames.AuthOtherSessionsSignedOut).Surface.ShouldBe(UsageSurface.Web);
    }
}
