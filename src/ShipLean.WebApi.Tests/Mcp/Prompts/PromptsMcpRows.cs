using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Prompts;

// PART is the seeded project Joel is not in; a key no member can see answers as one that does not exist.
public sealed class PromptsMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("prompts", McpScopes.Read, ProjectRole.Member, """{"project":"PART"}""", ReadOnly: true),
        new("save_prompt", McpScopes.Plan, ProjectRole.Admin, """{"project":"PART","prompt":"guide","text":"Sweep.","note":"Sweep.","based_on":"built-in"}""", ReadOnly: true),
        new("use_prompt_version", McpScopes.Write, ProjectRole.Admin, """{"project":"PART","prompt":"guide","version":"built-in"}"""),
    ];
}
