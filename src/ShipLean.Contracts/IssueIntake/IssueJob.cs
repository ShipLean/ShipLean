namespace ShipLean.Contracts.IssueIntake;

// A claimed issue_job. Payload is its JSON. Attempts counts claims, this one included. LeaseToken identifies this claim: pass it
// to CompleteAsync or FailAsync, which do nothing once another worker has claimed the job again.
public sealed record IssueJob(Guid Id, IssueJobKind Kind, string Key, Guid GitHubIssueId, Guid ProjectId, string Payload, int Attempts, DateTimeOffset CreatedAt, Guid LeaseToken);
