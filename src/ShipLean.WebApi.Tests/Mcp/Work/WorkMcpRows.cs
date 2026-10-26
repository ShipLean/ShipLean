using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;

namespace ShipLean.WebApi.Tests.Mcp.Work;

public sealed class WorkMcpRows : IMcpSweepRows
{
    public IEnumerable<McpSweepRow> Rows { get; } =
    [
        new("find_work_to_pick_up", "shiplean:read", ProjectRole.Member, """{"project":"PART"}""", ReadOnly: true),
        new("pick_up", "shiplean:write", ProjectRole.Member, """{"item":"PART-1"}"""),
        new("update_progress", "shiplean:write", ProjectRole.Member, """{"item":"PART-1","note":"PR is up"}"""),
        new("hand_back", "shiplean:write", ProjectRole.Member, """{"item":"PART-1"}"""),
    ];
}
