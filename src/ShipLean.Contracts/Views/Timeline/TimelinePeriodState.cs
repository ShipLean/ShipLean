namespace ShipLean.Contracts.Views.Timeline;

// The kind of state a task sat in for a period of its history; StateName on the period is the workflow's own name.
public enum TimelinePeriodState
{
    New,
    Active,
    Blocked,
    Resolved,
    Closed,
    Removed,
}
