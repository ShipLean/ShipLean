using System.Collections.Concurrent;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;

namespace ShipLean.WebApi.Realtime;

// Which user each hub connection joined which project as, so a member removed from a project can be dropped from its
// group. Groups know their connections but not their users; this keeps the other half, per process, as the groups
// themselves are. Removals reach every instance through IMemberRemovals.
[RegisterSingleton]
public sealed class ProjectGroupMembers(IHubContext<ProjectHub> hub, IPresenceTracker presence)
{
    private readonly ConcurrentDictionary<(string ConnectionId, Guid ProjectId), Guid> _joined = new();

    public void Joined(string connectionId, Guid userId, Guid projectId) => _joined[(connectionId, projectId)] = userId;

    public void Left(string connectionId, Guid projectId) => _joined.TryRemove((connectionId, projectId), out _);

    public void Disconnected(string connectionId)
    {
        foreach (var key in _joined.Keys.Where(k => k.ConnectionId == connectionId))
        {
            _joined.TryRemove(key, out _);
        }
    }

    // Every (project, user) that some connection here has joined, each once.
    public IReadOnlyList<(Guid ProjectId, Guid UserId)> Memberships()
        => _joined.Select(entry => (entry.Key.ProjectId, entry.Value)).Distinct().ToList();

    // Takes every connection of userId on this instance out of the project's group, so nothing from the project reaches
    // them again, and off the project's screens (the other members are told).
    public async Task RemoveUserAsync(Guid projectId, Guid userId)
    {
        foreach (var (key, user) in _joined)
        {
            if (key.ProjectId == projectId && user == userId && _joined.TryRemove(key, out _))
            {
                await hub.Groups.RemoveFromGroupAsync(key.ConnectionId, ProjectHub.GroupName(projectId));
            }
        }
        await presence.RemoveUserAsync(projectId, userId);
    }
}
