using StackExchange.Redis;

namespace ShipLean.WebApi.Realtime;

// The Redis that SHIPLEAN_REDIS_CONNECTION names, for the realtime state the SignalR backplane does not carry: presence
// (RedisPresenceTracker, PresenceHeartbeat) and member removals (RedisMemberRemovals). Registered only when that is set.
// Every key and channel here starts with Prefix: the connection string's channelPrefix, else "shiplean", the prefix
// the backplane's channels get too. InstanceId names this process in the presence entries it owns.
public sealed class RedisRealtime : IAsyncDisposable
{
    public const string DefaultPrefix = "shiplean";

    private readonly Lazy<Task<ConnectionMultiplexer>> _connection;

    public RedisRealtime(string configuration)
    {
        var options = ConfigurationOptions.Parse(configuration);
        Prefix = PrefixOf(options);
        // The names below carry the prefix themselves; StackExchange.Redis would glue it on with no separator.
        options.ChannelPrefix = default;
        // A Redis that is down when the app starts is retried in the background rather than failing the host.
        options.AbortOnConnectFail = false;
        _connection = new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(options));
    }

    public string Prefix { get; }

    public string InstanceId { get; } = Guid.NewGuid().ToString("N");

    public static string PrefixOf(ConfigurationOptions options)
        => options.ChannelPrefix is { IsNullOrEmpty: false } prefix ? prefix.ToString() : DefaultPrefix;

    public async Task<IDatabase> DatabaseAsync() => (await _connection.Value).GetDatabase();

    public async Task<ISubscriber> SubscriberAsync() => (await _connection.Value).GetSubscriber();

    public RedisChannel MembersRemovedChannel => RedisChannel.Literal($"{Prefix}:members-removed");

    // connection id -> "<user id> <instance id>", for one screen of one project.
    public RedisKey ScreenKey(Guid projectId, string screenId) => $"{Prefix}:presence:{ScreenMember(projectId, screenId)}";

    // The ScreensKey member for a screen; ParseScreenMember reads it back.
    public static string ScreenMember(Guid projectId, string screenId) => $"{projectId}:{screenId}";

    public static (Guid ProjectId, string ScreenId) ParseScreenMember(string member)
    {
        var colon = member.IndexOf(':');
        return (Guid.Parse(member.AsSpan(0, colon)), member[(colon + 1)..]);
    }

    // Every screen that has anyone on it, as ScreenMember values.
    public RedisKey ScreensKey => $"{Prefix}:presence-screens";

    // One counter for every presence change on every screen: presenceChanged.sequence.
    public RedisKey SequenceKey => $"{Prefix}:presence-sequence";

    // Every instance that has registered a heartbeat, live or not yet cleaned up.
    public RedisKey InstancesKey => $"{Prefix}:presence-instances";

    // Exists while that instance is alive (PresenceHeartbeat refreshes it).
    public RedisKey AliveKey(string instanceId) => $"{Prefix}:presence-alive:{instanceId}";

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            await (await _connection.Value).DisposeAsync();
        }
    }
}
