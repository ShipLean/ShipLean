namespace ShipLean.Contracts.IssueIntake;

// What a github_outbox entry does on GitHub.
public enum OutboxKind
{
    Comment,
    Close,
    Reopen,
}
