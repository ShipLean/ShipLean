namespace ShipLean.Contracts.Views.InspectionMetrics;

// A point on one task's measured time, as the rail draws it.
public enum InspectionEventKind
{
    Started,
    Commit,
    Resolved,
    Blocked,
    Unblocked,
    ReviewApproved,
    ReviewChangesRequested,
}
