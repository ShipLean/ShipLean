namespace ShipLean.Application.Features.IssueIntake.Outbox;

// A leased github_outbox entry and the issue it writes to. Attempts counts claims, this one included. LeaseToken is the stored
// leased_until, which the claim's writes must match.
internal sealed record OutboxClaim(
    Guid Id,
    Guid GitHubIssueId,
    string Kind,
    string? Body,
    string? Reason,
    int Attempts,
    string? LastResult,
    DateTimeOffset CreatedAt,
    DateTimeOffset LeaseToken,
    string Host,
    string Repository,
    int Number,
    bool OpenOnGitHub);
