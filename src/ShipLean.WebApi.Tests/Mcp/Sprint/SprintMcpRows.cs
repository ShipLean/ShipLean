using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

// PART is the seeded project Joel is not in. plan_sprint and end_sprint are called without a plan, so they only preview.
public sealed class SprintMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("plan_sprint", McpScopes.Plan, ProjectRole.Member, """{"project":"PART","sprint":"next"}""", ReadOnly: true),
        new("end_sprint", McpScopes.Plan, ProjectRole.Member, """{"project":"PART","sprint":"current"}""", ReadOnly: true),
        new("create_sprint", McpScopes.Plan, ProjectRole.Member, """{"project":"PART"}"""),
    ];
}
