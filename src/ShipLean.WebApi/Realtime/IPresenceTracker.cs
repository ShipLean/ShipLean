namespace ShipLean.WebApi.Realtime;

// Who is on which screen, per hub connection. A connection is on at most one screen per project (joining a second
// leaves the first). Every change publishes PresenceChanged with everyone now on that screen (a user with two tabs on
// it is listed once) and a sequence number that grows with every change, so a client keeps the newest list when two
// arrive out of order. A closed connection leaves its screens.
//
// PresenceTracker keeps it in this process; RedisPresenceTracker, when SHIPLEAN_REDIS_CONNECTION is set, shares it
// between instances. RealtimeServices registers one.
public interface IPresenceTracker
{
    Task JoinAsync(string connectionId, Guid userId, Guid projectId, string screenId);

    Task LeaveAsync(string connectionId, Guid projectId, string screenId);

    Task LeaveProjectAsync(string connectionId, Guid projectId);

    Task DisconnectedAsync(string connectionId);

    // Takes a user's connections on this instance off every screen of the project (they were removed from it),
    // publishing the new lists.
    Task RemoveUserAsync(Guid projectId, Guid userId);

    // How many screens have someone of this instance on them (a metric, so a cheap read, not a snapshot).
    int ScreenCount { get; }
}
