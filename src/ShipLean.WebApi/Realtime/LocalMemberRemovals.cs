namespace ShipLean.WebApi.Realtime;

// IMemberRemovals for a single instance: its own connections are all there are.
public sealed class LocalMemberRemovals(ProjectGroupMembers members) : IMemberRemovals
{
    public Task RemovedAsync(Guid projectId, Guid userId) => members.RemoveUserAsync(projectId, userId);
}
