using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// PART is the seeded project Joel is not in; its items are not seeded, and a key no member can see answers as one that does not exist.
public sealed class CaptureMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("capture_work", McpScopes.Write, ProjectRole.Member, """{"project":"PART","items":[{"type":"story","title":"Sweep"}]}"""),
        new("refine_work_item", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","title":"Sweep"}"""),
        new("break_down", McpScopes.Write, ProjectRole.Member, """{"parent":"PART-1","children":[{"title":"Sweep"}]}"""),
        new("comment", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","text":"Sweep"}"""),
        new("link", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","url":"https://example.com/pr/1","title":"Sweep"}"""),
    ];
}
