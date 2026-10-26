using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Contracts.Agents;

namespace ShipLean.UnitTest.Contracts.Agents;

public sealed class InMemoryAgentConsentTests : AgentConsentContract
{
    private InMemoryAgentConsentWorld _world = null!;

    [SetUp]
    public void CreateWorld() => _world = new InMemoryAgentConsentWorld();

    protected override AgentWorld World => _world;

    protected override IAgentConsent ConsentFor(Guid userId) => new ActingAs(_world, userId);

    protected override Guid ArrangeRequest(Guid userId, string clientId, string redirectUri, string? state, IReadOnlyList<string> scopes)
        => _world.Consent.Add(userId, clientId, redirectUri, state, AgentWorld.Challenge, scopes, AgentWorld.Resource, Issuer);

    // Names the user on every call, since the fake reads the one current user.
    private sealed class ActingAs(InMemoryAgentConsentWorld world, Guid userId) : IAgentConsent
    {
        public Task<ConsentRequestDto> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
            => world.As(userId).GetAsync(requestId, cancellationToken);

        public Task<ConsentResult> DecideAsync(Guid requestId, ConsentDecision decision, CancellationToken cancellationToken = default)
            => world.As(userId).DecideAsync(requestId, decision, cancellationToken);
    }
}
