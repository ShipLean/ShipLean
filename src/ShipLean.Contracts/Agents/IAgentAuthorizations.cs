namespace ShipLean.Contracts.Agents;

// A user's consent to one client: also the "Connected agents" read.
public interface IAgentAuthorizations
{
    // Stores the user's current security stamp, so a later stamp change ends the authorization's tokens. A user has one
    // live authorization per client: when one exists and its stored stamp is still the user's current one it is updated
    // (scopes become the union, so a re-consent never narrows them) and its id returned, so a re-consent adds no second
    // row and existing tokens keep working. When the stamp has changed since, the old authorization and all its tokens are
    // revoked and a new one (new id, the requested scopes only) is created: a stamp change is never undone. Throws
    // NotFound for an unknown user or client.
    Task<Guid> CreateAsync(Guid userId, string clientId, IReadOnlySet<string> scopes, CancellationToken cancellationToken = default);

    // The user's authorizations that are not revoked, newest first.
    Task<IReadOnlyList<ConnectedAgentDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    // Revokes the authorization and so its tokens. Throws NotFound for an unknown id or another user's.
    Task RevokeAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
