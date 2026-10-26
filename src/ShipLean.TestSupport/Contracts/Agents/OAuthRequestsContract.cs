using Shouldly;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using static ShipLean.TestSupport.Contracts.Agents.AgentWorld;

namespace ShipLean.TestSupport.Contracts.Agents;

// Behaviour every IOAuthRequests has: a created request is the one IAgentConsent then describes to its user.
[TestFixture]
public abstract class OAuthRequestsContract
{
    protected abstract AgentWorld World { get; }

    private static OAuthRequestCreate Request(Guid userId, string clientId)
        => new(clientId, LoopbackRedirect, "st-1", Challenge, ["shiplean:write"], Resource, userId);

    [Test]
    public async Task Create_ThenConsentGet_ShouldDescribeIt()
    {
        var userId = await World.ArrangeUserAsync();
        var clientId = await World.RegisterClientAsync("Request Agent");

        var requestId = await World.Requests.CreateAsync(Request(userId, clientId));

        var described = await World.ConsentFor(userId).GetAsync(requestId);
        described.RequestId.ShouldBe(requestId);
        described.ClientName.ShouldBe("Request Agent");
        described.Verified.ShouldBeFalse();
        described.RedirectHost.ShouldBe("127.0.0.1");
        described.RequestedScopes.ShouldBe(["shiplean:read", "shiplean:write"]);
        described.LocalhostOnly.ShouldBeTrue();
    }

    [Test]
    public async Task Create_WhenClientUnknown_ShouldBeNotFound()
    {
        var userId = await World.ArrangeUserAsync();

        var error = await Should.ThrowAsync<ApiException>(() => World.Requests.CreateAsync(Request(userId, "no-such-client")));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenUserUnknown_ShouldBeNotFound()
    {
        var clientId = await World.RegisterClientAsync();

        var error = await Should.ThrowAsync<ApiException>(() => World.Requests.CreateAsync(Request(Guid.NewGuid(), clientId)));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenLifetimePassed_ShouldBeNotFoundOnConsent()
    {
        var userId = await World.ArrangeUserAsync();
        var requestId = await World.Requests.CreateAsync(Request(userId, await World.RegisterClientAsync()));

        World.Advance(OAuthRequestCreate.Lifetime + TimeSpan.FromSeconds(1));

        var error = await Should.ThrowAsync<ApiException>(() => World.ConsentFor(userId).GetAsync(requestId));
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
