using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Attach;

// PART is the seeded project Joel is not in; its items are not seeded, and a key no member can see answers as one that does not exist.
public sealed class AttachMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("attach_markdown", McpScopes.Write, ProjectRole.Member, """{"feature":"PART-1","name":"sweep.md","content":"Sweep"}"""),
        new("attach_file", McpScopes.Write, ProjectRole.Member, """{"feature":"PART-1","name":"sweep.txt","content_type":"text/plain","base64":"U3dlZXA="}"""),
    ];
}
