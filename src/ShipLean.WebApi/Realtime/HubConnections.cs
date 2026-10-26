using System.Collections.Concurrent;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;

namespace ShipLean.WebApi.Realtime;

// The hub connections that are open in this process, so the session sweep can end the ones whose session has ended.
[RegisterSingleton]
public sealed class HubConnections
{
    private readonly ConcurrentDictionary<string, HubCallerContext> _open = new();

    public void Opened(HubCallerContext context) => _open[context.ConnectionId] = context;

    public void Closed(string connectionId) => _open.TryRemove(connectionId, out _);

    public IReadOnlyCollection<HubCallerContext> Snapshot() => _open.Values.ToList();
}
