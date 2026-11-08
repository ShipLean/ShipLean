using System.Data.Common;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.InboxWorker;

// The real github_outbox, except that while CrashAfterEnqueue is set an enqueue writes its row on the caller's transaction and then
// throws, like a worker dying halfway: an entry that survives was written outside the caller's transaction. AfterEnqueue runs
// after each enqueue, for a test that changes the world mid-job (another worker taking the job's lease).
internal sealed class CrashingGitHubOutbox(DbGitHubOutbox inner) : IGitHubOutbox
{
    public bool CrashAfterEnqueue { get; set; }

    public Func<Task>? AfterEnqueue { get; set; }

    public async Task<bool> EnqueueAsync(OutboxEntry entry, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var added = await inner.EnqueueAsync(entry, transaction, cancellationToken);
        if (AfterEnqueue is { } after)
        {
            await after();
        }

        return CrashAfterEnqueue ? throw new InvalidOperationException("Crashed after the enqueue.") : added;
    }
}
