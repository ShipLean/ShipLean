using Microsoft.Extensions.DependencyInjection;

namespace ShipLean.LocalEnvironment.Hosting;

// Port: 0 is a free port on 127.0.0.1. Above 0 the app binds http://localhost:<port>, so both IPv4 and IPv6 loopback
// answer (Vite proxies to localhost:5113). ListenOnAllInterfaces binds 0.0.0.0 for port 0, for a peer that is not loopback.
// RedisConnection: SHIPLEAN_REDIS_CONNECTION, for hosts that act as instances of one deployment.
// Settings: more app configuration, applied last.
// ConfigureServices runs after the profile's removals, so it can add a removed service back or swap a double in.
public sealed record AppHostOptions
{
    public required string ConnectionString { get; init; }

    public int Port { get; init; }

    public bool ListenOnAllInterfaces { get; init; }

    public string? RedisConnection { get; init; }

    public IReadOnlyDictionary<string, string?>? Settings { get; init; }

    public HostProfile Profile { get; init; } = HostProfile.Test;

    public Action<IServiceCollection>? ConfigureServices { get; init; }
}
