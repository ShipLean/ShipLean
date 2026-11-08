namespace ShipLean.Contracts.IssueIntake;

// The "issues" actions ShipLean acts on; every other action is Ignored. Deleted and Transferred mean the issue is gone from
// its repository: intake stops treating it as open on GitHub, and the work item stays as it is.
public enum IssueAction
{
    Opened,
    Edited,
    Closed,
    Reopened,
    Deleted,
    Transferred,
}
