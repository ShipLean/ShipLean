namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// Everything the renderer needs, as plain values, so it depends on no table. AppUserId is the connection's id
// (sent as appUserId and oauthClientId). TriggerCommentId marks the thread the session was started from, if any.
public sealed record AgentContextInput(
    string SessionId,
    string AppUserId,
    DateTimeOffset SessionCreatedAt,
    AgentUserInput Creator,
    AgentProjectInput Project,
    AgentRepositoryInput Repository,
    AgentItemInput Item,
    AgentRelatedItemInput? Parent,
    AgentRelatedItemInput? Feature,
    IReadOnlyList<AgentCommentInput> Comments,
    string? TriggerCommentId);
