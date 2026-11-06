namespace ShipLean.Application.Features.Backups.Runs;

// The backup_run log and the one-run-at-a-time lease (decision D8). Rows are never deleted.
public interface IBackupRunLog
{
    // Takes the lease (a session advisory lock on a connection of its own), marks every lock-free Running row abandoned,
    // then inserts the new Running row. A Scheduled start whose occurrence already has a row is OccurrenceTaken.
    Task<BackupStartResult> TryStartAsync(BackupStart start, CancellationToken cancellationToken = default);

    // False when the row is no longer Running (it was marked abandoned, or already finished).
    Task<bool> SucceedAsync(Guid id, BackupOutcome outcome, CancellationToken cancellationToken = default);

    Task<bool> FailAsync(Guid id, string error, CancellationToken cancellationToken = default);

    // Succeeded, unpruned runs written to this destination, newest first.
    Task<IReadOnlyList<BackupRunRow>> UnprunedSuccessesAsync(string destinationId, CancellationToken cancellationToken = default);

    // Sets pruned_at on this destination's succeeded, unpruned runs beyond the newest keepCount, in one statement on the
    // lease's own connection, and returns them (newest first): the caller deletes only these objects. Throws
    // BackupLeaseLostException, claiming nothing, when the lease is lost.
    Task<IReadOnlyList<BackupRunRow>> ClaimPruneAsync(BackupLease lease, string destinationId, int keepCount, CancellationToken cancellationToken = default);

    // The newest runs, newest first, without Skipped rows.
    Task<IReadOnlyList<BackupRunRow>> RecentAsync(int count, CancellationToken cancellationToken = default);

    Task<BackupRunRow?> LastSuccessAsync(CancellationToken cancellationToken = default);

    // The latest scheduled_for of any Scheduled row (run, failed or skipped): the schedule's cursor.
    Task<DateTimeOffset?> LatestOccurrenceAsync(CancellationToken cancellationToken = default);

    // Records an occurrence that never started as a Skipped row. Does nothing when the occurrence already has a row.
    Task RecordSkippedAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken = default);
}
