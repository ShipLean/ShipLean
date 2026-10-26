using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Backlog;

// PART is the seeded project Joel is not in; a key or project there answers exactly like an unknown one.
public sealed class BacklogMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("review_backlog", McpScopes.Read, ProjectRole.Member, """{"project":"PART"}""", ReadOnly: true),
        new("sprint_review", McpScopes.Read, ProjectRole.Member, """{"project":"PART","sprint":"current"}""", ReadOnly: true),
        new("move_to_sprint", McpScopes.Plan, ProjectRole.Member, """{"project":"PART","items_to_move":[{"item":"PART-1"}],"sprint":"backlog"}"""),
        new("reorder", McpScopes.Plan, ProjectRole.Member, """{"item":"PART-1","above":"PART-2"}"""),
    ];
}
