using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Reviews;

// PART is the seeded project Joel is not in: a key there answers like an unknown one.
public sealed class ReviewMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("log_review", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","verdict":"approved"}"""),
    ];
}
