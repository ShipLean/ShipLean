using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Instance.AgentConnections;

// The agent connection routes: instance admin only.
public sealed class AgentConnectionsRows : IAccessSweepRows
{
    private const string Body = """{"name":"Relay","receiverUrl":"https://relay.example.com/hook"}""";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/instance/agent-connections", ProjectRole.Member, InstanceScoped: true),
        new("POST", "api/instance/agent-connections", ProjectRole.Member, Body: Body, InstanceScoped: true),
        new("PUT", "api/instance/agent-connections/{id:guid}", ProjectRole.Member, Body: Body,
            Url: $"api/instance/agent-connections/{AgentConnectionSweepIds.Live}", InstanceScoped: true),
        new("POST", "api/instance/agent-connections/{id:guid}/regenerate-secret", ProjectRole.Member,
            Url: $"api/instance/agent-connections/{AgentConnectionSweepIds.Live}/regenerate-secret", InstanceScoped: true),
        new("POST", "api/instance/agent-connections/{id:guid}/regenerate-token", ProjectRole.Member,
            Url: $"api/instance/agent-connections/{AgentConnectionSweepIds.Live}/regenerate-token", InstanceScoped: true),
        new("POST", "api/instance/agent-connections/{id:guid}/archive", ProjectRole.Member,
            Url: $"api/instance/agent-connections/{AgentConnectionSweepIds.ToArchive}/archive", InstanceScoped: true),
    ];
}
