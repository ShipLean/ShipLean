using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Planning;

// PART is the seeded project Joel is not in; its items are not seeded, and a key no member can see answers as one that does not exist.
public sealed class PlanningMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("planning_guide", McpScopes.Read, ProjectRole.Member, """{"project":"PART"}""", ReadOnly: true),
        new("capture_plan", McpScopes.Write, ProjectRole.Member, """{"project":"PART","plan":"sealed","feature":{"title":"x"},"shared_branch":"x","common_brief":"x","stories":[]}"""),
        new("get_brief", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1"}""", ReadOnly: true),
        new("report_task", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","status":"done"}"""),
        new("plan_status", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1"}""", ReadOnly: true),
    ];
}
