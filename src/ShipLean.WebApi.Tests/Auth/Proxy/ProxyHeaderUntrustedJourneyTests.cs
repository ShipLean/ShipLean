using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Hosting.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIP-63 J1: plain-header mode reached from a peer that is neither loopback nor a trusted proxy.
[TestFixture]
public class ProxyHeaderUntrustedJourneyTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool ListenOnAllInterfaces => true;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => TestIdentityProvider.HeaderSettings();

    [Test]
    public async Task HeaderSignIn_WhenFromUntrustedPeer_ShouldAnswerUnauthorized()
    {
        var email = $"intruder-{Guid.NewGuid():N}@shiplean.test";
        using var client = ProxyClient(new Uri($"http://{LocalAddresses.OtherThanLoopback()}:{new Uri(Host.BaseUrl).Port}"))
            .WithHeaders(("Remote-Email", email), ("Remote-Name", "In Truder"));

        using var response = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        await using var db = await OpenDatabaseAsync();
        (await db.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe(0);
    }
}
