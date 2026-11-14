using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Code;

// PART is the seeded project Joel is not in; its items are not seeded, and a key no member can see answers as one that does not exist.
public sealed class CodeMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("add_commits", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","commits":[{"repo":"acme/storefront","sha":"a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8"}]}"""),
    ];
}
