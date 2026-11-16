namespace ShipLean.Contracts.Agents;

// An agent connection's recent deliveries, for the instance Agents settings row: its newest success, how many sends failed
// in the last 24 hours, and its newest failure with that failure's scrubbed error. Labels are server labels.
public sealed record AgentConnectionHealthDto(
    DateTimeOffset? LastDeliveredAt,
    string? LastDeliveredLabel,
    int FailedLast24h,
    DateTimeOffset? LastFailedAt,
    string? LastFailedLabel,
    string? LastError);
