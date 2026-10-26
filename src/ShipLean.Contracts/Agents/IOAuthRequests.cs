namespace ShipLean.Contracts.Agents;

// /oauth/authorize's write side: the pending request IAgentConsent then reads and decides.
public interface IOAuthRequests
{
    // Returns the request id, which expires after OAuthRequestCreate.Lifetime. Throws NotFound for an unknown client or user.
    Task<Guid> CreateAsync(OAuthRequestCreate request, CancellationToken cancellationToken = default);
}
