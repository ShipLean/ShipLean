namespace ShipLean.Contracts.Views.Timeline;

// An arrow between two items of this Feature (D6, D19). WaitLabel is set only when WaitHours is 12 or more. UnblockedAt and
// UnblockedHours belong to Blocked edges, and are null while To is still blocked (drawn to the end of To's box).
public sealed record TimelineEdgeDto(
    string From,
    string To,
    TimelineEdgeKind Kind,
    TimelineEdgeSource Source,
    double? WaitHours,
    string? WaitLabel,
    DateTimeOffset? UnblockedAt,
    double? UnblockedHours,
    bool OnCriticalPath);
