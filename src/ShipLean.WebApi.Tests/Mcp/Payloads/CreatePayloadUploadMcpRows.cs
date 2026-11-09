using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

public sealed class CreatePayloadUploadMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows =>
    [
        new("create_payload_upload", McpScopes.Write, ProjectRole.Member, """{"tool":"capture_work"}""",
            NoProject: "Names no project: the payload is bound to the caller, their agent authorization and the tool, and the tool that uses it checks project access itself."),
    ];
}
