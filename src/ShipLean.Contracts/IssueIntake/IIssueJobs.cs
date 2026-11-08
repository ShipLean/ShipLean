using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// Durable multi-step work (issue_job, D18). Enqueue in the transaction of the state change that asks for the work; a worker
// claims a due job with a lease, then completes or fails it. A job whose worker died is claimed again once its lease ends.
public interface IIssueJobs
{
    // A claimed job is leased for this long.
    static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    // A job fails for good after this many claims.
    const int MaxAttempts = 6;

    // False when a job with this key already exists (it is left as it is). payloadJson is a JSON document, '{}' when null.
    Task<bool> EnqueueAsync(
        IssueJobKind kind, string key, Guid issueId, Guid projectId, string? payloadJson, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // The oldest due job of this kind, leased to the caller (FOR UPDATE SKIP LOCKED), or null when none is due.
    Task<IssueJob?> ClaimNextAsync(IssueJobKind kind, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid jobId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Retries after 1 min, 10 min, 1 h, then 6 h; after MaxAttempts claims the job is failed for good.
    Task FailAsync(Guid jobId, string error, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // How many jobs of this kind the project enqueued since `since` (the rate limit, D16).
    Task<int> CountSinceAsync(Guid projectId, IssueJobKind kind, DateTimeOffset since, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
