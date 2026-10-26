using Microsoft.AspNetCore.SignalR;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// A hub context with only groups, for building ProjectGroupMembers without a host.
internal sealed class FakeHubContext(IGroupManager groups) : IHubContext<ProjectHub>
{
    public IHubClients Clients => throw new NotSupportedException();

    public IGroupManager Groups { get; } = groups;
}
