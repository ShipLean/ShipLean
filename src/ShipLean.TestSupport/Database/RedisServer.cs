using Testcontainers.Redis;

namespace ShipLean.TestSupport.Database;

// The Redis server the multi-instance realtime tests share. SHIPLEAN_TEST_REDIS (host:port of a long-lived server) wins;
// otherwise one redis:7.4 container is started and reused across runs. Tests keep apart by channel prefix
// (ScenarioConnectionString), not by server.
public static class RedisServer
{
    private static readonly Lazy<Task<string>> Server = new(StartAsync);

    // A SHIPLEAN_REDIS_CONNECTION value of its own: every key and channel the app uses starts with a fresh prefix.
    public static async Task<string> ScenarioConnectionStringAsync()
        => $"{await ConnectionStringAsync()},channelPrefix=shiplean-test-{Guid.NewGuid():N}";

    // The server alone (host:port), for a test that reads or writes keys itself.
    public static Task<string> ConnectionStringAsync() => Server.Value;

    private static async Task<string> StartAsync()
    {
        var configured = Environment.GetEnvironmentVariable("SHIPLEAN_TEST_REDIS");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }
        var container = new RedisBuilder("redis:7.4")
            .WithReuse(true)
            .WithLabel("shiplean-tests", "redis-7.4")
            .Build();
        await container.StartAsync();
        return container.GetConnectionString();
    }
}
