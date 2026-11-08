namespace ShipLean.Contracts.IssueIntake;

// GitHub's state_reason for a close: completed when the item is done, not_planned when it was removed.
public enum CloseReason
{
    Completed,
    NotPlanned,
}
