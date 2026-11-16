namespace ShipLean.Contracts.Views.InspectionMetrics;

// One bug counted in an opened period (SHIP-1398 QK3): found before release ones included, so the list can show them struck out.
// Severity is 1 (critical) to 4 (low), null when unset. FiledLabel is made on the server in the viewer's zone. CausedByKey and
// CausedByTitle name the Task its Caused by link points at (item D18), both null without one.
public sealed record InspectionBugDto(
    string Key,
    string Title,
    int? Severity,
    DateTimeOffset FiledAt,
    string FiledLabel,
    bool FoundBeforeRelease,
    string? CausedByKey,
    string? CausedByTitle);
