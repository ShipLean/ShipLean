using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// One hub connection and the events it has received. "Receives nothing" is proved without waiting: a later event for
// another project goes through the same relay in order, so once that arrives everything earlier would already have
// been delivered.
public sealed class HubReceiver : IAsyncDisposable
{
    private readonly Channel<JsonElement> _events = Channel.CreateUnbounded<JsonElement>();

    public HubReceiver(ShipLeanHost host, Guid? user = null, Action<HttpConnectionOptions>? configure = null)
    {
        Connection = new HubConnectionBuilder()
            .WithUrl(host.BaseUrl + ProjectHub.Path, options =>
            {
                if (user is { } id)
                {
                    options.Headers[TestAuthentication.UserHeader] = id.ToString();
                }
                configure?.Invoke(options);
            })
            .Build();
        Connection.On<JsonElement>(ProjectHub.EventMethod, e => _events.Writer.TryWrite(e));
    }

    public HubConnection Connection { get; }

    public static async Task<HubReceiver> StartAsync(ShipLeanHost host, Guid? user = null, Action<HttpConnectionOptions>? configure = null)
    {
        var receiver = new HubReceiver(host, user, configure);
        await receiver.Connection.StartAsync();
        return receiver;
    }

    public async Task<JsonElement> NextAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _events.Reader.ReadAsync(timeout.Token);
    }

    // The next event of that type, skipping others: a previous test's disconnects can still be on their way through the
    // relay when this test starts.
    public Task<JsonElement> NextAsync(string type) => NextWhereAsync(e => e.GetProperty("type").GetString() == type);

    // The next presence list for that screen, skipping everything else.
    public Task<JsonElement> NextPresenceAsync(string screenId)
        => NextWhereAsync(e => e.GetProperty("type").GetString() == "presenceChanged" && e.GetProperty("screenId").GetString() == screenId);

    private async Task<JsonElement> NextWhereAsync(Func<JsonElement, bool> match)
    {
        while (true)
        {
            var next = await NextAsync();
            if (match(next))
            {
                return next;
            }
        }
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
