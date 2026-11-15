using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// PART is not Joel's project, so the three tools answer not_found (and while Inspection is off, not_found for everyone).
public sealed class InspectionMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("list_runs", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1"}""", ReadOnly: true),
        new("get_run", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1","run":"sweep-run"}""", ReadOnly: true),
        new("agent_setup", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1","agent":"claude-code"}""", ReadOnly: true),
    ];
}
