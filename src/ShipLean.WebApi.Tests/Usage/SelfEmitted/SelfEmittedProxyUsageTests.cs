using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.SelfEmitted;

// Proxy sign-in tracks auth.signed_in when it issues a cookie, and only then.
[TestFixture]
public class SelfEmittedProxyUsageTests : ProxyApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task ProxySignIn_WhenHeaderUserArrives_ShouldTrackSignedIn()
    {
        using var client = ProxyClient().WithIdentity($"proxy-{Guid.NewGuid():N}@shiplean.test", "Ada Lovelace");

        using var response = await client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.AuthSignedIn);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.Kind.ShouldBe(UsageActorKind.User);
        tracked.Actor.UserId.ShouldNotBeNull();
        tracked.Source.ShouldBe("system:ProxySignInHandler");
    }

    [Test]
    public async Task ProxySignIn_WhenCookieAlreadyIssued_ShouldTrackOnlyTheFirst()
    {
        using var client = ProxyClient().WithIdentity($"proxy-{Guid.NewGuid():N}@shiplean.test");
        using var first = await client.GetAsync("/api/me");

        using var again = await client.GetAsync("/api/me");

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        Recorder.Events.Count(e => e.Name == UsageEventNames.AuthSignedIn).ShouldBe(1);
    }
}
