namespace ShipLean.Application.Features.LinearApi;

// An agent session as the Linear-compatible API shows it. State is ShipLean's (submitted, working, inputRequired, completed,
// failed, canceled, stale). ExternalUrls and Plan are the stored jsonb documents as text.
public sealed record LinearSession(
    Guid Id,
    Guid WorkItemId,
    string ItemKey,
    string ProjectKey,
    string State,
    string ExternalUrls,
    string Plan,
    DateTimeOffset LastActivityAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndedAt,
    LinearPerson Creator);
