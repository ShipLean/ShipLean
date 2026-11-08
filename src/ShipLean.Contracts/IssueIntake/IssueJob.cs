namespace ShipLean.Contracts.IssueIntake;

// A claimed issue_job. Payload is its JSON. Attempts counts claims, this one included.
public sealed record IssueJob(Guid Id, IssueJobKind Kind, string Key, Guid GitHubIssueId, Guid ProjectId, string Payload, int Attempts, DateTimeOffset CreatedAt);
