namespace ShipLean.Contracts.Agents;

// One send of a session's delivery, as the panel and MCP show it: per session, oldest first. Action is the delivery's action
// (created or prompted). Error is scrubbed (AgentErrorText) and never holds the delivery body. Labels are server labels.
public sealed record AgentDeliveryAttemptDto(
    string Action,
    int Attempt,
    DateTimeOffset At,
    string AtLabel,
    int? HttpStatus,
    string? Error,
    int DurationMs,
    DateTimeOffset? NextAttemptAt,
    string? NextAttemptLabel);
