using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Timeline;

// PART is the seeded project Joel is not in: a key there answers like an unknown one.
public sealed class TimelineMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("feature_timeline", McpScopes.Read, ProjectRole.Member, """{"key":"PART-1"}""", ReadOnly: true),
    ];
}
