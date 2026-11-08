namespace ShipLean.Application.Features.IssueIntake.Outbox;

// A leased github_outbox entry and the issue it writes to. Attempts counts claims, this one included. LeaseToken is the new
// lease_token this claim set, which every write it makes must match.
internal sealed record OutboxClaim(
    Guid Id,
    Guid GitHubIssueId,
    string Kind,
    string? Body,
    string? Reason,
    int Attempts,
    string? LastResult,
    DateTimeOffset CreatedAt,
    Guid LeaseToken,
    string Host,
    string Repository,
    int Number);
