using System.Collections.Concurrent;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// How many times each probe tool actually ran.
public sealed class ProbeLog
{
    private readonly ConcurrentDictionary<string, int> _runs = new();

    public void Count(string tool) => _runs.AddOrUpdate(tool, 1, (_, n) => n + 1);

    public void Reset() => _runs.Clear();

    public int Runs(string tool) => _runs.GetValueOrDefault(tool);
}
