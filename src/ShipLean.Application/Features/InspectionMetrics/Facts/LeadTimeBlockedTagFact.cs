namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// A revision of a task that added or removed the Blocked tag; BlockedAfter says which.
public sealed record LeadTimeBlockedTagFact(string ItemKey, DateTimeOffset At, int Rev, bool BlockedAfter);
