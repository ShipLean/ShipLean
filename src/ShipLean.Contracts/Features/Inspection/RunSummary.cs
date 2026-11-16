namespace ShipLean.Contracts.Inspection;

// A run is one agent session; RunId is the session id and Agent is service.name.
public sealed record RunSummary(
    string RunId,
    string SessionId,
    string Agent,
    string? StartedBy,
    IReadOnlyList<string> Models,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int SpanCount,
    int LogCount,
    int ErrorCount,
    long? TokensIn,
    long? TokensOut,
    IReadOnlyList<RunItemLink> Links);
