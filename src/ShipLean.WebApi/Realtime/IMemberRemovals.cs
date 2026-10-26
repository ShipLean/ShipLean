namespace ShipLean.WebApi.Realtime;

// Called after a member's removal from a project commits (removed by an admin, or left): takes that user's hub
// connections out of the project's group and off its screens on every instance, so nothing from the project reaches
// them again. LocalMemberRemovals for one instance; RedisMemberRemovals fans it out when SHIPLEAN_REDIS_CONNECTION is
// set. RealtimeServices registers one. HubSessionSweep is the backstop for a removal that never arrives.
public interface IMemberRemovals
{
    Task RemovedAsync(Guid projectId, Guid userId);
}
