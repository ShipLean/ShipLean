namespace ShipLean.Contracts.Agents;

// One agent session as the panel draws it. State is the agent_session state (submitted, working, inputRequired, completed, failed,
// canceled, stale); Live is true for submitted, working and inputRequired. StopRequested is true once Stop was pressed on a live
// session. Times are ISO strings with server labels. EndedLabel is null while the session has not ended. Activities run oldest
// first; ephemeral ones are dropped unless the newest activity is one, which is kept alone.
public sealed record AgentSessionDto(
    Guid Id,
    string ConnectionName,
    string State,
    bool Live,
    bool StopRequested,
    DateTimeOffset StartedAt,
    string StartedLabel,
    DateTimeOffset LastActivityAt,
    string LastActivityLabel,
    string? EndedLabel,
    IReadOnlyList<AgentUrlDto> Urls,
    IReadOnlyList<AgentPlanStepDto> Plan,
    IReadOnlyList<AgentActivityDto> Activities);
