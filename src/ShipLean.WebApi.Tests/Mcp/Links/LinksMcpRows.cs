using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Links;

// No link carries this address in any project Joel is in, so the report finds nothing to apply to and answers as an unknown address does.
public sealed class LinksMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("report_link_state", McpScopes.Write, ProjectRole.Member, """{"url":"https://part.example/pull/1","as_of":"2026-10-25T09:00:00Z","state":"merged"}"""),
    ];
}
