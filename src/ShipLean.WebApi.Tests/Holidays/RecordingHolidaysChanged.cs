using ShipLean.Contracts.Holidays;

namespace ShipLean.WebApi.Tests.Holidays;

// Remembers the country codes the refresh job reported as changed.
public sealed class RecordingHolidaysChanged : IHolidaysChanged
{
    private readonly List<string> _codes = [];
    private readonly SemaphoreSlim _arrived = new(0);

    public IReadOnlyList<string> Codes
    {
        get
        {
            lock (_codes)
            {
                return [.. _codes];
            }
        }
    }

    // Completes when one more country has been reported changed since the last call; fails after 20 seconds.
    public async Task NextAsync()
    {
        if (!await _arrived.WaitAsync(TimeSpan.FromSeconds(20)))
        {
            throw new TimeoutException("No holiday change was reported.");
        }
    }

    public Task ChangedAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        lock (_codes)
        {
            _codes.Add(countryCode);
        }

        _arrived.Release();

        return Task.CompletedTask;
    }
}
