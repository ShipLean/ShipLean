namespace ShipLean.Contracts.IssueIntake;

// One GitHub write to deliver. A Comment needs Body (Markdown that already passed PublicTextGuard when an agent wrote it; the
// deliverer adds the outbox marker), a Close needs Reason, and a Reopen needs neither. DedupeKey names the write, so enqueuing
// the same write twice (a retried job, a replayed delivery) keeps one entry.
public sealed record OutboxEntry(Guid GitHubIssueId, OutboxKind Kind, string DedupeKey, string? Body = null, CloseReason? Reason = null)
{
    public static OutboxEntry Comment(Guid gitHubIssueId, string dedupeKey, string body) => new(gitHubIssueId, OutboxKind.Comment, dedupeKey, body);

    public static OutboxEntry Close(Guid gitHubIssueId, string dedupeKey, CloseReason reason) => new(gitHubIssueId, OutboxKind.Close, dedupeKey, Reason: reason);

    public static OutboxEntry Reopen(Guid gitHubIssueId, string dedupeKey) => new(gitHubIssueId, OutboxKind.Reopen, dedupeKey);
}
