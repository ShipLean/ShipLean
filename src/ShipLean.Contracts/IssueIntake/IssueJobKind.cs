namespace ShipLean.Contracts.IssueIntake;

// What an issue_job does: start a round (a proposal session), process a proposal session's response, or act on an LGTM.
public enum IssueJobKind
{
    Round,
    Proposal,
    Accept,
}
