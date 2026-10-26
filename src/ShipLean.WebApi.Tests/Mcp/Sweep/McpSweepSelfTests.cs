using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Sweep;

// The sweep fails when it should: on a host with the probe tools registered and no rows for them.
[TestFixture]
public class McpSweepSelfTests : McpFixture
{
    [Test]
    public void McpAccessSweep_WhenToolHasNoRow_ShouldFail()
    {
        var tools = McpSweep.ToolNames(Host.Services);

        var rows = McpSweep.AllRows();

        var problems = McpSweep.RowProblems(tools, rows);

        tools.ShouldContain("probe_write");
        problems.ShouldContain("tool probe_write has no sweep row");
        problems.Count.ShouldBe(tools.Count(tool => rows.All(row => row.Tool != tool)));
    }

    [Test]
    public void McpAccessSweep_WhenRowNamesNoTool_ShouldFail()
    {
        var rows = new[] { new McpSweepRow("no_such_tool", McpScopes.Read, ProjectRole.Member) };

        var problems = McpSweep.RowProblems([], rows);

        problems.ShouldBe(["sweep row names no_such_tool, which is not a registered tool"]);
    }

    [Test]
    public async Task McpAccessSweep_WhenCallIsNotRefusedAsNotFound_ShouldFail()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var rows = new[]
        {
            new McpSweepRow("probe_not_found", McpScopes.Read, ProjectRole.Member, ReadOnly: true),
            new McpSweepRow("probe_whoami", McpScopes.Read, ProjectRole.Member, ReadOnly: true),
            new McpSweepRow("probe_ambiguous", McpScopes.Read, ProjectRole.Member, ReadOnly: true),
        };

        var problems = await McpSweep.CallProblemsAsync((tool, arguments) => CallToolAsync(token, tool, arguments), rows);

        problems.ShouldBe(["probe_whoami answered ok to Joel; expected not_found", "probe_ambiguous answered ambiguous to Joel; expected not_found"]);
    }
}
