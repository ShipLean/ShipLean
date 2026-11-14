using System.Text.Json;
using ShipLean.Application.Features.TaskChanges.Hosts;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;
using ShipLean.WebApi.Realtime;
using StackExchange.Redis;

namespace ShipLean.WebApi.Features.TaskChanges;

// The one place the patch cache is chosen, when the services are registered: Redis when SHIPLEAN_REDIS_CONNECTION is set (so
// every instance shares what one read), otherwise the memory of this instance.
public sealed class CodePatchCacheServices : IServiceModule
{
    public const int MaxMemoryEntries = 500;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration[RealtimeServices.RedisConnectionKey];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            // A factory, not an instance, so the container disposes the connection.
            services.AddSingleton<ICodePatchCache>(_ => new RedisCodePatchCache(redis));
        }
        else
        {
            services.AddSingleton<ICodePatchCache>(sp => new MemoryCodePatchCache(sp.GetRequiredService<TimeProvider>(), MaxMemoryEntries));
        }
    }
}

// Patches as JSON under a prefixed key with a Redis expiry, so the day and the five minutes are kept by Redis itself.
public sealed class RedisCodePatchCache : ICodePatchCache, IAsyncDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer>> _connection;
    private readonly string _prefix;

    public RedisCodePatchCache(string configuration)
    {
        var options = ConfigurationOptions.Parse(configuration);
        _prefix = $"{RedisRealtime.PrefixOf(options)}:code-patch:";
        options.ChannelPrefix = default;
        options.AbortOnConnectFail = false;
        _connection = new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(options));
    }

    public async Task<PatchRead?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var value = await (await _connection.Value).GetDatabase().StringGetAsync(_prefix + key);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PatchRead>((string)value);
    }

    public async Task SetAsync(string key, PatchRead value, TimeSpan lifetime, CancellationToken cancellationToken)
        => await (await _connection.Value).GetDatabase().StringSetAsync(_prefix + key, JsonSerializer.Serialize(value), lifetime);

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            await (await _connection.Value).DisposeAsync();
        }
    }
}
