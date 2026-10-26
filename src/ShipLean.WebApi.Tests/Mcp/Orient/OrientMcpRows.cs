using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

public sealed class OrientMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("list_projects", McpScopes.Read, ProjectRole.Member, """{"project":"PART"}""", ReadOnly: true),
        new("get_work_item", McpScopes.Read, ProjectRole.Member, """{"key":"PART-1"}""", ReadOnly: true),
        new("read_attachment", McpScopes.Read, ProjectRole.Member, """{"item":"PART-1","name":"sweep.md"}""", ReadOnly: true),
        new("search", McpScopes.Read, ProjectRole.Member, """{"words":"anything","project":"PART"}""", ReadOnly: true),
    ];
}
