using System.Data.Common;
using Dapper;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Outbox;

// github_outbox enqueue over Dapper, in the caller's transaction. A per-issue advisory lock comes first and is held to the end of
// that transaction, so the entries of one issue get their seq and commit in the same order; the unique dedupe_key makes a
// second enqueue of the same write a no-op. A new entry is due at once.
public sealed class DbGitHubOutbox(TimeProvider clock) : IGitHubOutbox
{
    public async Task<bool> EnqueueAsync(OutboxEntry entry, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(transaction);
        var (kind, reason) = Validate(entry);
        var connection = transaction.Connection ?? throw new ArgumentException("The transaction has no connection.", nameof(transaction));

        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('github_outbox:' || @issueId::text, 0))",
            new { issueId = entry.GitHubIssueId },
            transaction,
            cancellationToken: cancellationToken));

        var now = clock.GetUtcNow();
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO github_outbox (id, github_issue_id, kind, body, reason, dedupe_key, next_attempt_at, created_at)
            VALUES (@id, @issueId, @kind, @body, @reason, @dedupeKey, @now, @now)
            ON CONFLICT (dedupe_key) DO NOTHING
            """,
            new { id = Guid.CreateVersion7(), issueId = entry.GitHubIssueId, kind, body = entry.Body, reason, dedupeKey = entry.DedupeKey, now },
            transaction,
            cancellationToken: cancellationToken)) == 1;
    }

    // The stored kind and reason (GitHub's state_reason words), after checking the entry carries what its kind needs.
    private static (string Kind, string? Reason) Validate(OutboxEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DedupeKey))
        {
            throw new ArgumentException("An outbox entry needs a dedupe key.", nameof(entry));
        }

        return entry.Kind switch
        {
            OutboxKind.Comment when !string.IsNullOrWhiteSpace(entry.Body) && entry.Reason is null => ("comment", null),
            OutboxKind.Close when entry.Reason is { } reason && entry.Body is null => ("close", reason == CloseReason.Completed ? "completed" : "not_planned"),
            OutboxKind.Reopen when entry.Body is null && entry.Reason is null => ("reopen", null),
            _ => throw new ArgumentException($"A {entry.Kind} outbox entry has the wrong body or reason.", nameof(entry)),
        };
    }
}
