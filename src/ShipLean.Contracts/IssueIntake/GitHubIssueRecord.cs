namespace ShipLean.Contracts.IssueIntake;

// A GitHub issue a project took (github_issue). WorkItemId is its Triage item, or the item an acceptance repointed it to.
// Proposal is the latest proposal's JSON (C2) and ProposalSessionId the session that wrote it. LastPosted maps each progress
// milestone already posted to the value it was posted for.
public sealed record GitHubIssueRecord(
    Guid Id,
    IssueRef Issue,
    Guid ProjectId,
    Guid WorkItemId,
    string AuthorLogin,
    IssuePhase Phase,
    string? Proposal,
    Guid? ProposalSessionId,
    int Rounds,
    bool OpenOnGitHub,
    IReadOnlyDictionary<string, string> LastPosted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
