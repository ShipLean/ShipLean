using System.Collections.Concurrent;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Realtime;

// IPresenceTracker in memory, for a single instance (no SHIPLEAN_REDIS_CONNECTION).
//
// One gate per (project, screen) holds the change, the snapshot and the publish together. PublishAsync only enqueues,
// so the bus order then matches the snapshot order and the last event for a screen always lists everyone on it. The
// sequence is one counter for the process: a screen's events take it under that screen's gate, so it grows per screen.
public sealed class PresenceTracker(IProjectEvents events) : IPresenceTracker
{
    private long _sequence;

    private sealed class Screen
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        internal Dictionary<string, Guid> Connections { get; } = [];  // connection id -> user

        public bool Retired { get; set; }
    }

    private readonly ConcurrentDictionary<(Guid ProjectId, string ScreenId), Screen> _screens = new();

    // connection id -> (project -> the screen it is on)
    private readonly ConcurrentDictionary<string, Dictionary<Guid, string>> _current = new();

    public int ScreenCount => _screens.Values.Count(screen => !screen.Retired && screen.Connections.Count > 0);

    public async Task JoinAsync(string connectionId, Guid userId, Guid projectId, string screenId)
    {
        var previous = SetCurrent(connectionId, projectId, screenId);
        if (previous is not null && previous != screenId)
        {
            await ChangeAsync(projectId, previous, screen => screen.Connections.Remove(connectionId));
        }
        await ChangeAsync(projectId, screenId, screen => screen.Connections[connectionId] = userId);
    }

    public async Task LeaveAsync(string connectionId, Guid projectId, string screenId)
    {
        if (ClearCurrent(connectionId, projectId, screenId))
        {
            await ChangeAsync(projectId, screenId, screen => screen.Connections.Remove(connectionId));
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
        if (_current.TryRemove(connectionId, out var screens))
        {
            foreach (var (projectId, screenId) in screens.ToList())
            {
                await ChangeAsync(projectId, screenId, screen => screen.Connections.Remove(connectionId));
            }
        }
    }

    public async Task RemoveUserAsync(Guid projectId, Guid userId)
    {
        foreach (var screenId in _screens.Keys.Where(k => k.ProjectId == projectId).Select(k => k.ScreenId).ToList())
        {
            await ChangeAsync(projectId, screenId, screen =>
            {
                foreach (var connectionId in screen.Connections.Where(c => c.Value == userId).Select(c => c.Key).ToList())
                {
                    screen.Connections.Remove(connectionId);
                    ClearCurrent(connectionId, projectId, screenId);
                }
            });
        }
    }

    private async Task ChangeAsync(Guid projectId, string screenId, Action<Screen> change)
    {
        while (true)
        {
            var screen = _screens.GetOrAdd((projectId, screenId), _ => new Screen());
            await screen.Gate.WaitAsync();
            try
            {
                if (screen.Retired)
                {
                    continue;
                }
                change(screen);
                var userIds = screen.Connections.Values.Distinct().Order().ToList();
                await events.PublishAsync(new PresenceChanged(projectId, screenId, userIds, Interlocked.Increment(ref _sequence)));
                if (screen.Connections.Count == 0)
                {
                    screen.Retired = true;
                    _screens.TryRemove(new KeyValuePair<(Guid, string), Screen>((projectId, screenId), screen));
                }
                return;
            }
            finally
            {
                screen.Gate.Release();
            }
        }
    }

    private string? SetCurrent(string connectionId, Guid projectId, string screenId)
    {
        var screens = _current.GetOrAdd(connectionId, _ => []);
        lock (screens)
        {
            screens.TryGetValue(projectId, out var previous);
            screens[projectId] = screenId;
            return previous;
        }
    }

    private bool ClearCurrent(string connectionId, Guid projectId, string screenId)
    {
        if (!_current.TryGetValue(connectionId, out var screens))
        {
            return false;
        }
        lock (screens)
        {
            return screens.TryGetValue(projectId, out var current) && current == screenId && screens.Remove(projectId);
        }
    }

    private string? CurrentScreen(string connectionId, Guid projectId)
    {
        if (!_current.TryGetValue(connectionId, out var screens))
        {
            return null;
        }
        lock (screens)
        {
            return screens.GetValueOrDefault(projectId);
        }
    }
}
