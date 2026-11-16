namespace ShipLean.Contracts.Agents;

// One send to record (IAgentDeliveryAttempts.RecordAsync). Error is already scrubbed by the caller (AgentErrorText.Scrub); a
// success has a 2xx HttpStatus and a null Error. NextAttemptAt is when the next try is due, null when none is.
public sealed record AgentDeliveryAttemptCreate(
    Guid DeliveryId,
    Guid ConnectionId,
    int Attempt,
    DateTimeOffset At,
    int? HttpStatus,
    string? Error,
    int DurationMs,
    DateTimeOffset? NextAttemptAt);
