namespace ShipLean.Application.Features.Demos.Reads;

// What the status rules read: the sprint's demo fields and its demo item's latest session. EndedAt is when that session ended, or its last activity.
internal sealed record SprintDemoFacts(
    bool Closed,
    DateTimeOffset? RequestedAt,
    string? BlockedMessage,
    bool LiveSession,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? UploadExpiresAt,
    string? LatestSessionState,
    DateTimeOffset? LatestSessionEndedAt);
