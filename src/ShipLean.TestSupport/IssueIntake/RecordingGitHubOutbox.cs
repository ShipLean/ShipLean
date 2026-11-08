using System.Collections.Concurrent;
using System.Data.Common;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// An IGitHubOutbox that keeps entries in memory, in enqueue order, one per DedupeKey. It ignores the transaction: entries stay
// even when the caller's transaction rolls back, so tests of rollback use the real DbGitHubOutbox.
public sealed class RecordingGitHubOutbox : IGitHubOutbox
{
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<OutboxEntry> _entries = new();

    public IReadOnlyList<OutboxEntry> Entries => [.. _entries];

    public Task<bool> EnqueueAsync(OutboxEntry entry, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (!_keys.TryAdd(entry.DedupeKey, 0))
        {
            return Task.FromResult(false);
        }

        _entries.Enqueue(entry);
        return Task.FromResult(true);
    }
}
