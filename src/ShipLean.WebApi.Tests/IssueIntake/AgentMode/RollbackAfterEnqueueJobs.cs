using System.Data.Common;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.AgentMode;

// The real issue_job store, except that when FailAfterEnqueue is set an enqueue writes its row on the caller's transaction and then
// throws, so the caller rolls back. A job that survives that was written outside the caller's transaction.
internal sealed class RollbackAfterEnqueueJobs(DbIssueJobs inner) : IIssueJobs
{
    public bool FailAfterEnqueue { get; set; }

    public async Task<bool> EnqueueAsync(
        IssueJobKind kind, string key, Guid issueId, Guid projectId, string? payloadJson, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var added = await inner.EnqueueAsync(kind, key, issueId, projectId, payloadJson, transaction, cancellationToken);
        return FailAfterEnqueue ? throw new InvalidOperationException("Failed after the enqueue, to roll the caller back.") : added;
    }

    public Task<IssueJob?> ClaimNextAsync(IssueJobKind kind, CancellationToken cancellationToken = default) => inner.ClaimNextAsync(kind, cancellationToken);

    public Task CompleteAsync(Guid jobId, DbTransaction? transaction = null, CancellationToken cancellationToken = default) => inner.CompleteAsync(jobId, transaction, cancellationToken);

    public Task FailAsync(Guid jobId, string error, DbTransaction? transaction = null, CancellationToken cancellationToken = default) => inner.FailAsync(jobId, error, transaction, cancellationToken);

    public Task<int> CountSinceAsync(Guid projectId, IssueJobKind kind, DateTimeOffset since, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => inner.CountSinceAsync(projectId, kind, since, transaction, cancellationToken);
}
