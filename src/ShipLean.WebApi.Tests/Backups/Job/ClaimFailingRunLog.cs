using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.WebApi.Tests.Backups.Job;

// The real run log, except that ClaimPruneAsync throws once FailClaims is set, as a database error mid-prune would.
internal sealed class ClaimFailingRunLog(IBackupRunLog inner) : IBackupRunLog
{
    public bool FailClaims { get; set; }

    public Task<BackupStartResult> TryStartAsync(BackupStart start, CancellationToken cancellationToken = default) => inner.TryStartAsync(start, cancellationToken);

    public Task<bool> SucceedAsync(Guid id, BackupOutcome outcome, CancellationToken cancellationToken = default) => inner.SucceedAsync(id, outcome, cancellationToken);

    public Task<bool> FailAsync(Guid id, string error, CancellationToken cancellationToken = default) => inner.FailAsync(id, error, cancellationToken);

    public Task<IReadOnlyList<BackupRunRow>> UnprunedSuccessesAsync(string destinationId, CancellationToken cancellationToken = default)
        => inner.UnprunedSuccessesAsync(destinationId, cancellationToken);

    public Task<IReadOnlyList<BackupRunRow>> ClaimPruneAsync(BackupLease lease, string destinationId, int keepCount, CancellationToken cancellationToken = default)
        => FailClaims
            ? throw new InvalidOperationException("The claim failed.")
            : inner.ClaimPruneAsync(lease, destinationId, keepCount, cancellationToken);

    public Task<IReadOnlyList<BackupRunRow>> RecentAsync(int count, CancellationToken cancellationToken = default) => inner.RecentAsync(count, cancellationToken);

    public Task<BackupRunRow?> LastSuccessAsync(CancellationToken cancellationToken = default) => inner.LastSuccessAsync(cancellationToken);

    public Task<DateTimeOffset?> LatestOccurrenceAsync(CancellationToken cancellationToken = default) => inner.LatestOccurrenceAsync(cancellationToken);

    public Task RecordSkippedAsync(DateTimeOffset scheduledFor, CancellationToken cancellationToken = default) => inner.RecordSkippedAsync(scheduledFor, cancellationToken);
}
