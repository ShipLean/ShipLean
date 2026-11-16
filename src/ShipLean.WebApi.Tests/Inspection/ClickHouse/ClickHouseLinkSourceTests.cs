using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// How the views translate LinkRules (K3): a ShipLean MCP tool call that names an item links the run to it as FromToolCall.
[TestFixture(TestName = "ClickHouse link sources, SHIP-1556")]
[Category("SHIP-1556")]
public sealed class ClickHouseLinkSourceTests
{
    [Test]
    public async Task FindRuns_WhenARunOnlyCalledPickUp_LinksItFromTheToolCall()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        var migrator = scratch.Migrator();
        await migrator.EnsureAsync(CancellationToken.None);
        await scratch.AddSpanAsync(
            "pick", 0, 1, "Unset",
            new Dictionary<string, string> { ["tool_name"] = "mcp__shiplean__pick_up", ["tool_parameters"] = "{\"item\":\"SHIP-7\"}" });

        var search = await scratch.Finder(migrator).FindAsync(ScratchExporter.Query("SHIP-7"), CancellationToken.None);

        search.Runs.Count.ShouldBe(1, search.Problem);
        search.Runs[0].Links.ShouldBe([new RunItemLink("SHIP-7", LinkSource.FromToolCall)]);
    }

    [Test]
    public async Task FindRuns_WhenOtherCallsNameAnItem_LinksOnlyShipleanToolsWithAnItemKey()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        var migrator = scratch.Migrator();
        await migrator.EnsureAsync(CancellationToken.None);
        await scratch.AddSpanAsync("other", 0, 1, "Unset", new Dictionary<string, string> { ["tool_name"] = "Edit", ["tool_parameters"] = "{\"item\":\"SHIP-8\"}" });
        await scratch.AddSpanAsync("other", 1, 1, "Unset", new Dictionary<string, string> { ["tool_name"] = "mcp__shiplean__pick_up", ["tool_parameters"] = "not json {\"item\":\"SHIP-9\"}" });
        await scratch.AddSpanAsync("other", 2, 1, "Unset", new Dictionary<string, string> { ["tool_name"] = "mcp__other__shiplean__pick_up", ["tool_parameters"] = "{\"item\":\"SHIP-10\"}" });
        await scratch.AddSpanAsync("other", 3, 1, "Unset", new Dictionary<string, string> { ["tool_name"] = "x", ["mcp_server_name"] = "shiplean", ["tool_input"] = "{\"task\":\"SHIP-11\"}" });
        await scratch.AddLogAsync("other", 4, "INFO", new Dictionary<string, string> { ["tool_name"] = "shiplean__comment", ["input"] = "{\"key\":\"SHIP-12\"}" });

        var linked = new List<string>();
        foreach (var key in new[] { "SHIP-8", "SHIP-9", "SHIP-10", "SHIP-11", "SHIP-12" })
        {
            var search = await scratch.Finder(migrator).FindAsync(ScratchExporter.Query(key), CancellationToken.None);
            if (search.Runs.Count > 0)
            {
                linked.Add(key);
            }
        }

        linked.ShouldBe(["SHIP-11", "SHIP-12"]);
    }

    // Claude Code with tool details on: the tool_result log says "mcp_tool", names the server inside tool_parameters and has the input in tool_input.
    [Test]
    public async Task FindRuns_WhenAClaudeCodeToolResultNamesShipleanInItsParameters_LinksItFromTheToolCall()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        var migrator = scratch.Migrator();
        await migrator.EnsureAsync(CancellationToken.None);
        await scratch.AddLogAsync("cc", 0, "", new Dictionary<string, string>
        {
            ["tool_name"] = "mcp_tool", ["tool_parameters"] = "{\"mcp_server_name\":\"shiplean\",\"mcp_tool_name\":\"pick_up\"}", ["tool_input"] = "{\"item\":\"SHIP-21\"}",
        });
        await scratch.AddLogAsync("cc", 1, "", new Dictionary<string, string>
        {
            ["tool_name"] = "mcp_tool", ["tool_parameters"] = "{\"mcp_server_name\":\"other\",\"mcp_tool_name\":\"pick_up\"}", ["tool_input"] = "{\"item\":\"SHIP-22\"}",
        });

        var linked = await scratch.Finder(migrator).FindAsync(ScratchExporter.Query("SHIP-21"), CancellationToken.None);
        var other = await scratch.Finder(migrator).FindAsync(ScratchExporter.Query("SHIP-22"), CancellationToken.None);

        linked.Runs.Count.ShouldBe(1, linked.Problem);
        linked.Runs[0].Links.ShouldBe([new RunItemLink("SHIP-21", LinkSource.FromToolCall)]);
        other.Runs.ShouldBeEmpty();
    }
}
