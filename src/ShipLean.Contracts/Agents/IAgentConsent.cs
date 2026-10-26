namespace ShipLean.Contracts.Agents;

// The consent step of the authorization code flow, for the signed-in user (ICurrentUser). A pending request is made by
// /oauth/authorize, lives about ten minutes and is spent by the first decision.
// A request always has its user: /oauth/authorize sends a signed-out browser through sign-in before it writes the
// oauth_request row, so every request has a user_id. A request bound to no user is not_found, like another user's.
public interface IAgentConsent
{
    // Throws NotFound for an unknown or expired request, or one that belongs to another user (the same message).
    // RequestedScopes always includes shiplean:read, which is always granted, whether or not the client asked for it.
    Task<ConsentRequestDto> GetAsync(Guid requestId, CancellationToken cancellationToken = default);

    // Allow: the granted scopes are the requested ones that are ticked, plus shiplean:read, which is always granted; Scopes
    // may contain shiplean:read (a page shows it ticked and sends it) even when the client did not ask for it. Any other
    // ticked scope that was not requested throws Validation. Creates the authorization, issues a code for the request's
    // challenge, redirect and resource, and answers the redirect URI with code, state (when the request had one) and iss.
    // Deny: the redirect carries error=access_denied, state and iss. Either way the request is spent: a second decision
    // throws NotFound, as for GetAsync.
    Task<ConsentResult> DecideAsync(Guid requestId, ConsentDecision decision, CancellationToken cancellationToken = default);
}
