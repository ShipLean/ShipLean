using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Agents;

public sealed class AgentsMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("get_agent_session", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1"}""", ReadOnly: true),
    ];
}
