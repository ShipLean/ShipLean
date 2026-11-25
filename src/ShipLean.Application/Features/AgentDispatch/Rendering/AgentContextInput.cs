using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// Everything the renderer needs, as plain values, so it depends on no table. AppUserId is the connection's id
// (sent as appUserId and oauthClientId). TriggerCommentId marks the thread the session was started from, if any.
// Ancestors is the whole chain above the item, nearest first, each with its description; when set, the promptContext renders it in place of the parent.
// Guidance is the prompt of the automation (or the person) that started the session; Transition is the move that fired it.
// Mode is the session's mode; implement sends no shipleanMode in the `created` payload, propose and demo send theirs. A demo input is the
// implement one with Mode set to Demo (the demo prompt is its Guidance). Propose is set exactly when Mode is
// Propose: the loader then builds an allowlisted input (AgentContextLoader.LoadProposeAsync) and the promptContext is AgentProposePromptContext.
// In propose mode Repository is the GitHub issue's repository (rendered as shipleanRepository, not in promptContext) and Project.Name is blank.
// RunLogUrl is the session's one-use agent-log upload link (C10): RenderCreated adds it to the guidance as one line; null leaves the guidance as it is.
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
    IReadOnlyList<AgentRelatedItemInput>? Ancestors = null,
    AgentSessionMode Mode = AgentSessionMode.Implement,
    AgentProposeInput? Propose = null,
    string? RunLogUrl = null)
{
    // A demo session works from the sprint data in its guidance alone. The demo item's comments and attachments are earlier runs' output
    // (an old script that says "slides only", a failed run's report), so they are left out and can't steer a new run.
    public AgentContextInput AsDemo()
        => this with { Mode = AgentSessionMode.Demo, Comments = [], TriggerCommentId = null, Item = Item with { Attachments = [] } };
}
