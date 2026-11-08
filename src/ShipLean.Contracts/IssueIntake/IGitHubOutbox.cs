using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// github_outbox (D15): every GitHub write is an entry, enqueued in the transaction of the change that asks for it and delivered
// later, one at a time per issue, in seq order. Enqueue takes a per-issue advisory lock in that transaction first, so the
// entries of one issue commit in the order of their seq and a reader never sees a later entry before an earlier one commits.
public interface IGitHubOutbox
{
    // False when an entry with this DedupeKey already exists (it is left as it is). Throws ArgumentException for an entry
    // whose Kind lacks its Body or Reason.
    Task<bool> EnqueueAsync(OutboxEntry entry, DbTransaction transaction, CancellationToken cancellationToken = default);
}
