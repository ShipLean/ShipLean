namespace ShipLean.Contracts.IssueIntake;

// The "issues" actions ShipLean acts on; every other action is Ignored.
public enum IssueAction
{
    Opened,
    Edited,
    Closed,
    Reopened,
}
