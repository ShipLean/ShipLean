using System.Collections.Concurrent;

namespace ShipLean.Application.Features.Usage;

// A small in-memory cache for the usage lookups: entries expire after a fixed time and the cache never holds more than MaxEntries
// (when full it is emptied, which is cheap and keeps the code simple). Misses are not cached, so a row created a moment ago is found.
internal sealed class LookupCache<TKey, TValue>(TimeProvider time, TimeSpan lifetime, int maxEntries)
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, (TValue Value, DateTimeOffset ExpiresAt)> _entries = new();

    public bool TryGet(TKey key, out TValue value)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > time.GetUtcNow())
        {
            value = entry.Value;
            return true;
        }
        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        if (_entries.Count >= maxEntries)
        {
            _entries.Clear();
        }
        _entries[key] = (value, time.GetUtcNow() + lifetime);
    }
}
