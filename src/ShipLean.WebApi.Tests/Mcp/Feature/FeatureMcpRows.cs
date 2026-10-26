using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Feature;

// PART is the seeded project Joel is not in; its items are not seeded, and a key no member can see answers as one that does not exist.
public sealed class FeatureMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("set_status", McpScopes.Write, ProjectRole.Member, """{"feature":"PART-1","status":"Sweep"}"""),
        new("create_feature", McpScopes.Write, ProjectRole.Member, """{"project":"PART","title":"Sweep","goal":"Sweep"}"""),
    ];
}
