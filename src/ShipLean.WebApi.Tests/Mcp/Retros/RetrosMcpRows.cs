using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

// PART is the seeded project Joel is not in; a key no member can see answers as one that does not exist.
public sealed class RetrosMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("log_retrospective", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","went_well":[{"text":"Sweep"}]}"""),
        new("list_retrospectives", McpScopes.Read, ProjectRole.Member, """{"project":"PART"}""", ReadOnly: true),
        new("decide_retro_actions", McpScopes.Plan, ProjectRole.Member, """{"project":"PART","sprint":"current"}""", ReadOnly: true),
    ];
}
