using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

// issue_job over Dapper. A claim takes the oldest due pending job of a kind with FOR UPDATE SKIP LOCKED, so two workers never
// take the same one, and leases it under a new lease token; attempts counts claims, so a job whose worker keeps dying still
// runs out of attempts, and a job whose last lease ends without a complete or fail is failed by the next claim of its kind.
// Complete and fail are single statements that act only for the current lease token on a pending job, so a worker that
// overran its lease, or raced a complete, changes nothing. Times come from the TimeProvider, not the database clock, so the
// schedule follows one clock.
public sealed class DbIssueJobs(NpgsqlDataSource dataSource, TimeProvider clock) : IIssueJobs
{
    private const string LastLeaseExpired = "The lease expired on the last attempt.";

    private sealed record Row(Guid Id, string Kind, string Key, Guid GitHubIssueId, Guid ProjectId, string Payload, int Attempts, DateTimeOffset CreatedAt, Guid LeaseToken);

    public async Task<bool> EnqueueAsync(
        IssueJobKind kind, string key, Guid issueId, Guid projectId, string? payloadJson, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        return await dataSource.RunAsync(
            (connection, tx) => connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO issue_job (id, kind, key, github_issue_id, project_id, payload, next_attempt_at, created_at)
                VALUES (@id, @kind, @key, @issueId, @projectId, @payload::jsonb, @now, @now)
                ON CONFLICT (key) DO NOTHING
                """,
                new { id = Guid.CreateVersion7(), kind = IssueWire.Kind(kind), key, issueId, projectId, payload = payloadJson ?? "{}", now },
                tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken) == 1;
    }

    public async Task<IssueJob?> ClaimNextAsync(IssueJobKind kind, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var wireKind = IssueWire.Kind(kind);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE issue_job SET status = 'failed', last_error = @error, leased_until = NULL, lease_token = NULL, done_at = @now
            WHERE kind = @kind AND status = 'pending' AND attempts >= @max AND leased_until <= @now
            """,
            new { kind = wireKind, now, max = IIssueJobs.MaxAttempts, error = LastLeaseExpired },
            cancellationToken: cancellationToken));
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            WITH due AS (
                SELECT id FROM issue_job
                WHERE kind = @kind AND status = 'pending' AND next_attempt_at <= @now AND attempts < @max
                  AND (leased_until IS NULL OR leased_until <= @now)
                ORDER BY next_attempt_at, created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            UPDATE issue_job j SET leased_until = @until, lease_token = @token, attempts = j.attempts + 1
            FROM due WHERE j.id = due.id
            RETURNING j.id, j.kind, j.key, j.github_issue_id AS gitHubIssueId, j.project_id AS projectId, j.payload::text AS payload, j.attempts,
                      j.created_at AS createdAt, j.lease_token AS leaseToken
            """,
            new { kind = wireKind, now, until = now + IIssueJobs.Lease, max = IIssueJobs.MaxAttempts, token = Guid.NewGuid() },
            cancellationToken: cancellationToken));
        return row is null
            ? null
            : new IssueJob(row.Id, IssueWire.ParseKind(row.Kind), row.Key, row.GitHubIssueId, row.ProjectId, row.Payload, row.Attempts, row.CreatedAt, row.LeaseToken);
    }

    public async Task<bool> CompleteAsync(Guid jobId, Guid leaseToken, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => await dataSource.RunAsync(
            (connection, tx) => connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE issue_job SET status = 'done', done_at = @now, leased_until = NULL, lease_token = NULL
                WHERE id = @jobId AND lease_token = @leaseToken AND status = 'pending'
                """,
                new { jobId, leaseToken, now = clock.GetUtcNow() },
                tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken) == 1;

    // One statement: the backoff is read from attempts in the row it updates, so a complete in between cannot be undone.
    // The wait after the nth failed attempt is 1 min, 10 min, 1 h, then 6 h; the MaxAttempts-th fails the job for good.
    public async Task<bool> FailAsync(Guid jobId, Guid leaseToken, string error, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => await dataSource.RunAsync(
            (connection, tx) => connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE issue_job SET
                    status = CASE WHEN attempts >= @max THEN 'failed' ELSE 'pending' END,
                    done_at = CASE WHEN attempts >= @max THEN @now END,
                    next_attempt_at = @now + CASE
                        WHEN attempts <= 1 THEN interval '1 minute'
                        WHEN attempts = 2 THEN interval '10 minutes'
                        WHEN attempts = 3 THEN interval '1 hour'
                        ELSE interval '6 hours' END,
                    last_error = @error, leased_until = NULL, lease_token = NULL
                WHERE id = @jobId AND lease_token = @leaseToken AND status = 'pending'
                """,
                new { jobId, leaseToken, error, now = clock.GetUtcNow(), max = IIssueJobs.MaxAttempts },
                tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken) == 1;

    public Task<int> CountSinceAsync(Guid projectId, IssueJobKind kind, DateTimeOffset since, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => dataSource.RunAsync(
            (connection, tx) => connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT count(*)::int FROM issue_job WHERE project_id = @projectId AND kind = @kind AND created_at >= @since",
                new { projectId, kind = IssueWire.Kind(kind), since = since.ToUniversalTime() },
                tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);
}
