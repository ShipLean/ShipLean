using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.InspectionMetrics;

// PART is the seeded project Joel is not in: naming it answers like an unknown project.
public sealed class InspectionMetricsMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("inspection_metrics", McpScopes.Read, ProjectRole.Member, """{"project":"PART"}""", ReadOnly: true),
    ];
}
