namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// A live Bug of the project (QK4): when it was filed (work_item.created_at), its severity (1 critical to 4 low, null when unset),
// whether it is marked found before release (item D17), and the key of the Task its live Caused by link points at (item D18), null
// without one. That Task's own facts are in BugFacts.Causes.
public sealed record BugFact(string Key, string Title, int? Severity, DateTimeOffset FiledAt, bool FoundBeforeRelease, string? CausedByKey);
