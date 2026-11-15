using System.Collections.Concurrent;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// The patch cache of one instance. Entries expire by TimeProvider and it never holds more than maxEntries (when full it is
// emptied, which keeps the code simple).
public sealed class MemoryCodePatchCache(TimeProvider time, int maxEntries) : ICodePatchCache
{
    private readonly ConcurrentDictionary<string, (PatchRead Value, DateTimeOffset ExpiresAt)> _entries = new(StringComparer.Ordinal);

    public Task<PatchRead?> GetAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > time.GetUtcNow() ? entry.Value : null);

    public Task SetAsync(string key, PatchRead value, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (_entries.Count >= maxEntries)
        {
            _entries.Clear();
        }

        _entries[key] = (value, time.GetUtcNow() + lifetime);
        return Task.CompletedTask;
    }
}
