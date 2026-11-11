using System.Net;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Backups.Runs;

// backup_run over Postgres, with the lease of decision D8. leaseConnectionString is the app's connection string with
// pooling off: each lease opens a connection of its own and closes it when disposed. Everything else uses the pool.
public sealed class BackupRunLog(NpgsqlDataSource dataSource, string leaseConnectionString, TimeProvider time) : IBackupRunLog
{
    public const string AbandonedError = "Abandoned: the instance running it stopped.";

    private const string StartSql =
        """
        INSERT INTO backup_run (id, trigger, state, scheduled_for, started_at, started_by, passphrase_generation, host, destination_id)
        VALUES (@id, @trigger, 'running', @scheduledFor, @now, @userId, @generation, @host, @destinationId)
        ON CONFLICT (scheduled_for) WHERE trigger = 'scheduled' DO NOTHING
        RETURNING
        """ + " " + BackupRunColumns.Select;

    // One statement on the lease's connection: it claims nothing unless that session holds the lock, and fails outright
    // when the connection is gone. The CTEs' columns are named apart from backup_run's, so RETURNING stays unqualified.
    private const string ClaimPruneSql =
        "WITH held AS (" + BackupLeaseLock.HeldSql + " AS lock_held), " +
        """
        ranked AS (
            SELECT id AS ranked_id, row_number() OVER (ORDER BY started_at DESC, id DESC) AS position
            FROM backup_run
            WHERE state = 'succeeded' AND pruned_at IS NULL AND destination_id = @destinationId)
        UPDATE backup_run SET pruned_at = @now
        FROM ranked, held
        WHERE backup_run.id = ranked.ranked_id AND ranked.position > @keepCount AND held.lock_held
        RETURNING
        """ + " " + BackupRunColumns.Select;

    private const string UnprunedSql =
        "SELECT " + BackupRunColumns.Select + " FROM backup_run WHERE state = 'succeeded' AND pruned_at IS NULL AND destination_id = @destinationId ORDER BY started_at DESC, id DESC";

    private const string RecentSql =
        "SELECT " + BackupRunColumns.Select + " FROM backup_run WHERE state <> 'skipped' ORDER BY started_at DESC, id DESC LIMIT @count";

    private const string LastSuccessSql =
        "SELECT " + BackupRunColumns.Select + " FROM backup_run WHERE state = 'succeeded' ORDER BY started_at DESC, id DESC LIMIT 1";

    private static readonly string Host = Dns.GetHostName();

    public async Task<BackupStartResult> TryStartAsync(BackupStart start, CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(leaseConnectionString);
        var leased = false;
        try
        {
            await connection.OpenAsync(cancellationToken);
            var locked = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(BackupLeaseLock.TrySql, BackupLeaseLock.Parameters, cancellationToken: cancellationToken));
            if (!locked)
            {
                return new BackupStartResult(null, BackupRefusal.Busy);
            }
            var now = time.GetUtcNow();
            // Holding the lock, every Running row belongs to a run whose lease is gone.
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE backup_run SET state = 'failed', finished_at = @now, error = @error WHERE state = 'running'",
                new { now, error = AbandonedError }, cancellationToken: cancellationToken));
            var row = await connection.QuerySingleOrDefaultAsync<BackupRunColumns.Row>(new CommandDefinition(
                StartSql,
                new
                {
                    id = Guid.CreateVersion7(now), trigger = BackupRunWire.Trigger(start.Trigger), scheduledFor = start.ScheduledFor?.ToUniversalTime(), now,
                    userId = start.UserId, generation = start.PassphraseGeneration, host = Host, destinationId = start.DestinationId,
                },
                cancellationToken: cancellationToken));
            if (row is null)
            {
                await connection.ExecuteAsync(new CommandDefinition(BackupLeaseLock.UnlockSql, BackupLeaseLock.Parameters, cancellationToken: cancellationToken));
                return new BackupStartResult(null, BackupRefusal.OccurrenceTaken);
            }
            leased = true;
            return new BackupStartResult(new BackupLease(connection, row.ToRun(), time), null);
        }
        finally
        {
            if (!leased)
            {
                // Unpooled: closing ends the session and releases the lock if it was taken.
                await connection.DisposeAsync();
            }
        }
    }

    public async Task<bool> SucceedAsync(Guid id, BackupOutcome outcome, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE backup_run
            SET state = 'succeeded', finished_at = @now, database_object_key = @DatabaseObjectKey, database_bytes = @DatabaseBytes,
                attachments_copied = @AttachmentsCopied, attachments_bytes = @AttachmentsBytes
            WHERE id = @id AND state = 'running'
            """,
            new { id, now = time.GetUtcNow(), outcome.DatabaseObjectKey, outcome.DatabaseBytes, outcome.AttachmentsCopied, outcome.AttachmentsBytes },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> FailAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE backup_run SET state = 'failed', finished_at = @now, error = @error WHERE id = @id AND state = 'running'",
            new { id, now = time.GetUtcNow(), error }, cancellationToken: cancellationToken)) == 1;
    }

    public Task<IReadOnlyList<BackupRunRow>> UnprunedSuccessesAsync(string destinationId, CancellationToken cancellationToken = default)
        => QueryAsync(UnprunedSql, new { destinationId }, cancellationToken);

    public async Task<IReadOnlyList<BackupRunRow>> ClaimPruneAsync(BackupLease lease, string destinationId, int keepCount, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keepCount);
        var rows = (await lease.Connection.UseAsync(
            connection => connection.QueryAsync<BackupRunColumns.Row>(new CommandDefinition(
                ClaimPruneSql,
                new { @namespace = BackupLeaseLock.NAMESPACE, key = BackupLeaseLock.KEY, destinationId, keepCount, now = time.GetUtcNow() },
                cancellationToken: cancellationToken)),
            cancellationToken)).ToList();
        if (rows.Count == 0)
        {
            // Nothing claimed: tell "nothing to prune" apart from "the lock is gone".
            await lease.EnsureHeldAsync(cancellationToken);
        }
        return [.. rows.Select(r => r.ToRun()).OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)];
    }

    public Task<IReadOnlyList<BackupRunRow>> RecentAsync(int count, CancellationToken cancellationToken = default)
        => QueryAsync(RecentSql, new { count }, cancellationToken);

    public async Task<BackupRunRow?> LastSuccessAsync(CancellationToken cancellationToken = default)
        => (await QueryAsync(LastSuccessSql, null, cancellationToken)).SingleOrDefault();

    public async Task<DateTimeOffset?> LatestOccurrenceAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<DateTimeOffset?>(new CommandDefinition(
            "SELECT max(scheduled_for) FROM backup_run WHERE trigger = 'scheduled'", cancellationToken: cancellationToken));
    }

    public async Task RecordSkippedAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO backup_run (id, trigger, state, scheduled_for, started_at, finished_at, host)
            VALUES (@id, 'scheduled', 'skipped', @scheduledFor, @now, @now, @host)
            ON CONFLICT (scheduled_for) WHERE trigger = 'scheduled' DO NOTHING
            """,
            new { id = Guid.CreateVersion7(now), scheduledFor = scheduledFor.ToUniversalTime(), now, host = Host },
            cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<BackupRunRow>> QueryAsync(string sql, object? parameters, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<BackupRunColumns.Row>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToRun())];
    }
}
