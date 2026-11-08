using System.Data.Common;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.IssueIntake.Outbox;

// The github_outbox rows the deliverer works on, over Dapper. Only an issue's oldest pending entry (lowest seq) is ever claimed,
// so one issue's writes go out in order. PostgreSQL refuses DISTINCT ON with FOR UPDATE, so a claim is, in one transaction: pick
// issues whose oldest pending entry is due and not leased, take a transaction-scoped advisory lock on one of them (skipping an
// issue another worker holds), lease that issue's oldest pending entry by id with FOR UPDATE SKIP LOCKED, and commit. The lease
// is the per-issue lock while sending: the issue has a leased entry, so no other worker picks it until the lease runs out.
// leased_until, as stored, is the claim's token: every later write names it, so a worker that overran its lease changes nothing.
internal static class OutboxClaims
{
    // Longer than a post plus a reconcile of up to five list pages.
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private const int CandidateIssues = 20;

    public static async Task<OutboxClaim?> ClaimNextAsync(NpgsqlDataSource dataSource, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var issues = await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT o.github_issue_id FROM github_outbox o
            WHERE o.sent_at IS NULL AND o.failed_at IS NULL AND o.next_attempt_at <= @now AND (o.leased_until IS NULL OR o.leased_until <= @now)
              AND NOT EXISTS (
                  SELECT 1 FROM github_outbox x
                  WHERE x.github_issue_id = o.github_issue_id AND x.sent_at IS NULL AND x.failed_at IS NULL AND x.seq < o.seq)
            ORDER BY o.next_attempt_at, o.seq
            LIMIT @limit
            """,
            new { now, limit = CandidateIssues },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var issueId in issues)
        {
            if (await TryClaimAsync(connection, transaction, issueId, now, cancellationToken) is { } claim)
            {
                await transaction.CommitAsync(cancellationToken);
                return claim;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    // Sent: the claim still holds its lease. False when another worker took the entry over.
    public static async Task<bool> MarkSentAsync(NpgsqlDataSource dataSource, OutboxClaim claim, long? commentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE github_outbox SET sent_at = @now, github_comment_id = @commentId, last_result = 'ok', last_error = NULL, leased_until = NULL
            WHERE {Held}
            """,
            new { claim.Id, claim.LeaseToken, commentId, now },
            cancellationToken: cancellationToken)) == 1;
    }

    // Not done yet: due again at `next`, with what happened. result is 'retry' or 'ambiguous'.
    public static async Task<bool> BackOffAsync(
        NpgsqlDataSource dataSource, OutboxClaim claim, string result, string error, DateTimeOffset next, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE github_outbox SET next_attempt_at = @next, last_result = @result, last_error = @error, leased_until = NULL
            WHERE {Held}
            """,
            new { claim.Id, claim.LeaseToken, next, result, error },
            cancellationToken: cancellationToken)) == 1;
    }

    // Failed for good, on the caller's transaction.
    public static async Task<bool> FailAsync(DbTransaction transaction, OutboxClaim claim, string result, string error, DateTimeOffset now, CancellationToken cancellationToken)
        => await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE github_outbox SET failed_at = @now, last_result = @result, last_error = @error, leased_until = NULL
            WHERE {Held}
            """,
            new { claim.Id, claim.LeaseToken, now, result, error },
            transaction,
            cancellationToken: cancellationToken)) == 1;

    // Fails the issue's next pending entry when it is a close, so an issue is never closed after an update it should have carried
    // was lost. True when there was one.
    public static async Task<bool> HoldNextCloseAsync(DbTransaction transaction, Guid issueId, string error, DateTimeOffset now, CancellationToken cancellationToken)
        => await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE github_outbox SET failed_at = @now, last_result = 'failed', last_error = @error
            WHERE id = (
                SELECT id FROM github_outbox
                WHERE github_issue_id = @issueId AND sent_at IS NULL AND failed_at IS NULL
                ORDER BY seq
                LIMIT 1)
              AND kind = 'close'
            """,
            new { issueId, now, error },
            transaction,
            cancellationToken: cancellationToken)) == 1;

    // The issue's current item and who a ShipLean note on it is written as: the project's run-as member, else the item's creator.
    public static Task<NoteTarget?> NoteTargetAsync(DbTransaction transaction, Guid issueId, CancellationToken cancellationToken)
        => transaction.Connection!.QuerySingleOrDefaultAsync<NoteTarget>(new CommandDefinition(
            """
            SELECT w.id AS workItemId, w.project_id AS projectId, COALESCE(i.run_as_user_id, w.created_by) AS authorId
            FROM github_issue g
            JOIN work_item w ON w.id = g.work_item_id
            LEFT JOIN project_issue_intake i ON i.project_id = g.project_id
            WHERE g.id = @issueId
            """,
            new { issueId },
            transaction,
            cancellationToken: cancellationToken));

    // The entry is still this claim's: pending, and leased with the claim's token.
    private const string Held = "id = @Id AND leased_until = @LeaseToken AND sent_at IS NULL AND failed_at IS NULL";

    private static async Task<OutboxClaim?> TryClaimAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid issueId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var locked = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT pg_try_advisory_xact_lock(hashtextextended('github_outbox_delivery:' || @issueId::text, 0))",
            new { issueId },
            transaction,
            cancellationToken: cancellationToken));
        if (!locked)
        {
            return null;
        }

        var head = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM github_outbox WHERE github_issue_id = @issueId AND sent_at IS NULL AND failed_at IS NULL ORDER BY seq LIMIT 1",
            new { issueId },
            transaction,
            cancellationToken: cancellationToken));
        if (head is null)
        {
            return null;
        }

        return await connection.QuerySingleOrDefaultAsync<OutboxClaim>(new CommandDefinition(
            """
            WITH head AS (
                SELECT id FROM github_outbox
                WHERE id = @head AND sent_at IS NULL AND failed_at IS NULL AND next_attempt_at <= @now AND (leased_until IS NULL OR leased_until <= @now)
                FOR UPDATE SKIP LOCKED)
            UPDATE github_outbox o SET leased_until = @until, attempts = o.attempts + 1
            FROM head, github_issue g
            WHERE o.id = head.id AND g.id = o.github_issue_id
            RETURNING o.id, o.github_issue_id AS gitHubIssueId, o.kind, o.body, o.reason, o.attempts, o.last_result AS lastResult,
                      o.created_at AS createdAt, o.leased_until AS leaseToken, g.host, g.repository, g.number, g.open_on_github AS openOnGitHub
            """,
            new { head, now, until = now + Lease },
            transaction,
            cancellationToken: cancellationToken));
    }

    internal sealed record NoteTarget(Guid WorkItemId, Guid ProjectId, Guid? AuthorId);
}
