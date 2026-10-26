using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Agoda.IoC.Core;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// Failed token lookups per client network: the first FailedLookupsPerMinutePerIp failures in a fixed one-minute window are
// answered 401, the ones after it 429. It never looks at a request with a valid token, so an attacker behind a shared
// address cannot lock real agents out; it only slows guessing. An IPv6 client is counted by its /64 (one subscriber's
// whole range), an IPv4-mapped one as the IPv4 address. In memory per process; the count is atomic per network, and
// windows that have ended are dropped by a timer, not by the requests.
[RegisterSingleton]
public sealed class AgentFailureLimiter : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _clock;
    private readonly ITimer _cleanup;
    private readonly ConcurrentDictionary<string, Counter> _failures = new();

    public AgentFailureLimiter(TimeProvider clock)
    {
        _clock = clock;
        _cleanup = clock.CreateTimer(_ => Sweep(), null, Window, Window);
    }

    // Counts one failed lookup from the address; false when it is over the limit (the caller answers 429).
    public bool TryRecordFailure(IPAddress? address)
        => _failures.GetOrAdd(Network(address), _ => new Counter()).TryIncrement(_clock.GetUtcNow());

    public int TrackedNetworks => _failures.Count;

    public void Dispose() => _cleanup.Dispose();

    private static string Network(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    private void Sweep()
    {
        var now = _clock.GetUtcNow();
        foreach (var pair in _failures.Where(pair => pair.Value.IsStale(now)))
        {
            _failures.TryRemove(pair);
        }
    }

    private sealed class Counter
    {
        private readonly object _gate = new();
        private DateTimeOffset _start;
        private int _count;

        public bool TryIncrement(DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_count == 0 || now - _start >= Window)
                {
                    _start = now;
                    _count = 0;
                }
                _count++;
                return _count <= AgentPolicy.FailedLookupsPerMinutePerIp;
            }
        }

        public bool IsStale(DateTimeOffset now)
        {
            lock (_gate)
            {
                return now - _start >= Window;
            }
        }
    }
}
