using System.Data.Common;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.InboxWorker;

// The real github_outbox, except that while CrashAfterEnqueue is set an enqueue writes its row on the caller's transaction and then
// throws, like a worker dying halfway: an entry that survives was written outside the caller's transaction.
internal sealed class CrashingGitHubOutbox(DbGitHubOutbox inner) : IGitHubOutbox
{
    public bool CrashAfterEnqueue { get; set; }

    public async Task<bool> EnqueueAsync(OutboxEntry entry, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var added = await inner.EnqueueAsync(entry, transaction, cancellationToken);
        return CrashAfterEnqueue ? throw new InvalidOperationException("Crashed after the enqueue.") : added;
    }
}
