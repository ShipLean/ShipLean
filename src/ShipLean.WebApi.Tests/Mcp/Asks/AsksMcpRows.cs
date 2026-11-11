using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Asks;

// PART is the seeded project Joel is not in; a key no member can see answers as one that does not exist.
public sealed class AsksMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("record_decisions", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","decisions":[{"question":"Q?","recommended":"A"}]}"""),
        new("ask", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","text":"Why?"}"""),
        new("answer_decision", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","number":1,"action":"confirm"}"""),
        new("answer_question", McpScopes.Write, ProjectRole.Member, """{"item":"PART-1","number":1,"answer":"Because"}"""),
    ];
}
