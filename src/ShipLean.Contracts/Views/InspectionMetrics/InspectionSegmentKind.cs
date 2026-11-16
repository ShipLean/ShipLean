namespace ShipLean.Contracts.Views.InspectionMetrics;

// A stretch of one task's measured time. Active, Blocked and Rework add up to the lead time; UnresolvedTail (last commit to
// Resolved) is shown but not counted.
public enum InspectionSegmentKind
{
    Active,
    Blocked,
    Rework,
    UnresolvedTail,
}
