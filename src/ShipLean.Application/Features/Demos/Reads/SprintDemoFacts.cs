namespace ShipLean.Application.Features.Demos.Reads;

// What the status rules read: the sprint's demo fields and its demo item's latest session. EndedAt is when that session ended, or its last activity.
// EndReason and EndDetail are that session's, as AgentSessionVisibility lets the viewer see them. Message is the body of that session's last
// response or error activity, raw; the rule quotes only its excerpt.
internal sealed record SprintDemoFacts(
    bool Closed,
    DateTimeOffset? RequestedAt,
    string? BlockedMessage,
    bool LiveSession,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? UploadExpiresAt,
    string? LatestSessionState,
    DateTimeOffset? LatestSessionEndedAt,
    string? LatestSessionEndReason,
    string? LatestSessionEndDetail,
    string? LatestSessionMessage);
