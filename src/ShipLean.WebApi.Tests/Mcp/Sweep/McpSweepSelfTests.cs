using System.Text.Json;
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

        var problems = await McpSweep.CallProblemsAsync((tool, arguments) => CallToolAsync(token, tool, arguments), await InputSchemasAsync(token), rows);

        problems.ShouldBe(["probe_whoami answered ok to Joel; expected not_found", "probe_ambiguous answered ambiguous to Joel; expected not_found"]);
    }

    [Test]
    public async Task McpAccessSweep_WhenProjectToolSaysNoProject_ShouldFail()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var rows = new[] { new McpSweepRow("sprint_status", McpScopes.Read, ProjectRole.Member, ReadOnly: true, NoProject: "Says it names no project.") };

        var problems = await McpSweep.CallProblemsAsync((tool, arguments) => CallToolAsync(token, tool, arguments), await InputSchemasAsync(token), rows);

        problems.ShouldBe(["sprint_status takes a project argument, so it can't be a NoProject row"]);
    }

    [Test]
    public async Task McpAccessSweep_WhenKeyScopedToolSaysNoProject_ShouldFail()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var rows = new[] { new McpSweepRow("get_work_item", McpScopes.Read, ProjectRole.Member, ReadOnly: true, NoProject: "Says it names no project.") };

        var problems = await McpSweep.CallProblemsAsync((tool, arguments) => CallToolAsync(token, tool, arguments), await InputSchemasAsync(token), rows);

        problems.ShouldBe(["get_work_item is a NoProject row but is not in McpSweep.ProjectFreeTools"]);
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task McpAccessSweep_WhenNoProjectReasonIsBlank_ShouldFail(string reason)
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var rows = new[] { new McpSweepRow("probe_whoami", McpScopes.Read, ProjectRole.Member, ReadOnly: true, NoProject: reason) };

        var problems = await McpSweep.CallProblemsAsync((tool, arguments) => CallToolAsync(token, tool, arguments), await InputSchemasAsync(token), rows);

        problems.ShouldBe(["probe_whoami is a NoProject row with a blank reason"]);
    }

    private async Task<IReadOnlyDictionary<string, JsonElement>> InputSchemasAsync(string token)
        => McpSweep.InputSchemas(await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""));
}
