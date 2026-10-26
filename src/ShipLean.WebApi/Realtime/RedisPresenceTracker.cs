using System.Collections.Concurrent;
using ShipLean.Contracts.Events;
using StackExchange.Redis;

namespace ShipLean.WebApi.Realtime;

// IPresenceTracker shared between instances through Redis (SHIPLEAN_REDIS_CONNECTION). Each screen is a hash of
// connection id -> "<user id> <instance id>" (RedisRealtime.ScreenKey). One Lua script applies a change, takes the next
// sequence number and returns everyone on the screen, atomically for every instance, so no lock is needed; the
// sequence orders the published lists on the client. This instance still remembers which screens its own connections
// are on, to leave them when they close. Entries of an instance that died without leaving are removed by
// PurgeInstanceAsync, which PresenceHeartbeat runs once that instance's heartbeat has expired.
public sealed class RedisPresenceTracker(RedisRealtime redis, IProjectEvents events) : IPresenceTracker
{
    private const string Set = "set";
    private const string Remove = "remove";
    private const string Purge = "purge";

    // KEYS: the screen's hash, the sequence, the set of screens with anyone on them.
    // ARGV: the screen's member of that set; the mode; the connection id (set, remove) or instance id (purge); the
    // entry (set). Returns { sequence, entries changed, user id... } with each user id once.
    private const string Script =
        """
        local changed = 1
        if ARGV[2] == 'set' then
          redis.call('HSET', KEYS[1], ARGV[3], ARGV[4])
        elseif ARGV[2] == 'remove' then
          redis.call('HDEL', KEYS[1], ARGV[3])
        else
          changed = 0
          local entries = redis.call('HGETALL', KEYS[1])
          for i = 1, #entries, 2 do
            if string.match(entries[i + 1], '^%S+ (%S+)$') == ARGV[3] then
              redis.call('HDEL', KEYS[1], entries[i])
              changed = changed + 1
            end
          end
        end
        local result, seen = { 0, changed }, {}
        for _, entry in ipairs(redis.call('HVALS', KEYS[1])) do
          local user = string.match(entry, '^(%S+)')
          if not seen[user] then
            seen[user] = true
            result[#result + 1] = user
          end
        end
        if #result == 2 then
          redis.call('SREM', KEYS[3], ARGV[1])
        else
          redis.call('SADD', KEYS[3], ARGV[1])
        end
        result[1] = redis.call('INCR', KEYS[2])
        return result
        """;

    private sealed class Local(Guid userId)
    {
        public Guid UserId { get; } = userId;

        internal Dictionary<Guid, string> Screens { get; } = [];  // project -> the screen it is on
    }

    private readonly ConcurrentDictionary<string, Local> _current = new();

    public async Task JoinAsync(string connectionId, Guid userId, Guid projectId, string screenId)
    {
        var previous = SetCurrent(connectionId, userId, projectId, screenId);
        if (previous is not null && previous != screenId)
        {
            await ChangeAsync(projectId, previous, Remove, connectionId);
        }
        await ChangeAsync(projectId, screenId, Set, connectionId, $"{userId} {redis.InstanceId}");
    }

    public async Task LeaveAsync(string connectionId, Guid projectId, string screenId)
    {
        if (ClearCurrent(connectionId, projectId, screenId))
        {
            await ChangeAsync(projectId, screenId, Remove, connectionId);
        }
    }

    public async Task LeaveProjectAsync(string connectionId, Guid projectId)
    {
        if (CurrentScreen(connectionId, projectId) is { } screenId)
        {
            await LeaveAsync(connectionId, projectId, screenId);
        }
    }

    public async Task DisconnectedAsync(string connectionId)
    {
        if (_current.TryRemove(connectionId, out var local))
        {
            List<KeyValuePair<Guid, string>> screens;
            lock (local)
            {
                screens = local.Screens.ToList();
            }
            foreach (var (projectId, screenId) in screens)
            {
                await ChangeAsync(projectId, screenId, Remove, connectionId);
            }
        }
    }

    public async Task RemoveUserAsync(Guid projectId, Guid userId)
    {
        foreach (var (connectionId, local) in _current)
        {
            if (local.UserId == userId && CurrentScreen(connectionId, projectId) is { } screenId)
            {
                await LeaveAsync(connectionId, projectId, screenId);
            }
        }
    }

    // Takes every entry of a dead instance off every screen, publishing the screens that lost someone.
    public async Task PurgeInstanceAsync(string instanceId)
    {
        var database = await redis.DatabaseAsync();
        foreach (var member in await database.SetMembersAsync(redis.ScreensKey))
        {
            var (projectId, screenId) = RedisRealtime.ParseScreenMember(member.ToString());
            await ChangeAsync(projectId, screenId, Purge, instanceId);
        }
    }

    private async Task ChangeAsync(Guid projectId, string screenId, string mode, string target, string entry = "")
    {
        var database = await redis.DatabaseAsync();
        var result = (RedisResult[]?)await database.ScriptEvaluateAsync(
            Script,
            [redis.ScreenKey(projectId, screenId), redis.SequenceKey, redis.ScreensKey],
            [RedisRealtime.ScreenMember(projectId, screenId), mode, target, entry])
            ?? throw new InvalidOperationException("The presence script returned nothing.");
        if ((long)result[1] == 0)
        {
            return;
        }
        var userIds = result.Skip(2).Select(user => Guid.Parse(user.ToString())).Order().ToList();
        await events.PublishAsync(new PresenceChanged(projectId, screenId, userIds, (long)result[0]));
    }

    private string? SetCurrent(string connectionId, Guid userId, Guid projectId, string screenId)
    {
        var local = _current.GetOrAdd(connectionId, _ => new Local(userId));
        lock (local)
        {
            local.Screens.TryGetValue(projectId, out var previous);
            local.Screens[projectId] = screenId;
            return previous;
        }
    }

    private bool ClearCurrent(string connectionId, Guid projectId, string screenId)
    {
        if (!_current.TryGetValue(connectionId, out var local))
        {
            return false;
        }
        lock (local)
        {
            return local.Screens.TryGetValue(projectId, out var current) && current == screenId && local.Screens.Remove(projectId);
        }
    }

    private string? CurrentScreen(string connectionId, Guid projectId)
    {
        if (!_current.TryGetValue(connectionId, out var local))
        {
            return null;
        }
        lock (local)
        {
            return local.Screens.GetValueOrDefault(projectId);
        }
    }
}
