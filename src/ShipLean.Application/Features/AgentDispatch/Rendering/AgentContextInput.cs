namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// Everything the renderer needs, as plain values, so it depends on no table. AppUserId is the connection's id
// (sent as appUserId and oauthClientId). TriggerCommentId marks the thread the session was started from, if any.
// Ancestors is the whole chain above the item, nearest first, each with its description; when set, the promptContext renders it in place of the parent.
// Guidance is the prompt of the automation (or the person) that started the session; Transition is the move that fired it.
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
    string? TriggerCommentId,
    string? Guidance = null,
    AgentTransitionInput? Transition = null,
    IReadOnlyList<AgentRelatedItemInput>? Ancestors = null);
