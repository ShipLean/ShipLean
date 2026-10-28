using ShipLean.WebApi;

namespace ShipLean.UnitTest.Architecture.Mcp;

// Rules for the MCP tools. The scope and internal rules scan the whole WebApi assembly (Program.cs registers every
// [McpServerToolType] in it, wherever it sits); the data-access rule scans Features/Mcp/Tools. With no tools yet the production checks pass because there is nothing to
// break; the self-tests run the same scans over fixture types that do break each rule, so the rules can fail.
[TestFixture]
public class McpToolRulesTests
{
    private const string Fixtures = "ShipLean.UnitTest.Architecture.Mcp.Fixtures";

    [Test]
    public void Tools_ShouldAllCarryAnMcpScope()
    {
        McpToolScan.ToolsWithoutScope(typeof(ApiMarker).Assembly, McpToolScan.WholeAssembly).ShouldBeEmpty();
    }

    [Test]
    public void Tools_ShouldNotReferenceNpgsqlOrDapper()
    {
        McpToolScan.DataAccessReferences(typeof(ApiMarker).Assembly, McpToolScan.ToolsNamespace).ShouldBeEmpty();
    }

    [Test]
    public void ToolTypes_ShouldBeInternal()
    {
        McpToolScan.NonInternalToolTypes(typeof(ApiMarker).Assembly, McpToolScan.WholeAssembly).ShouldBeEmpty();
    }

    [Test]
    public void ToolResults_ShouldAllBeBuiltByMcpToolResult()
    {
        McpToolScan.HandBuiltToolResults(typeof(ApiMarker).Assembly, McpToolScan.WholeAssembly).ShouldBeEmpty();
    }

    [Test]
    public void Guard_WhenReplyHelperBuildsContentByHand_ShouldFail()
    {
        McpToolScan.HandBuiltToolResults(typeof(McpToolRulesTests).Assembly, Fixtures)
            .ShouldBe([$"{Fixtures}.Results.HandBuiltReply"]);
    }

    [Test]
    public void Guard_WhenToolLacksScope_ShouldFail()
    {
        McpToolScan.ToolsWithoutScope(typeof(McpToolRulesTests).Assembly, Fixtures)
            .ShouldBe([$"{Fixtures}.Bad.UnscopedTools.NoScope"]);
    }

    [Test]
    public void Guard_WhenToolReferencesDapperOrNpgsql_ShouldFail()
    {
        var violations = McpToolScan.DataAccessReferences(typeof(McpToolRulesTests).Assembly, Fixtures);

        violations.ShouldContain(v => v.StartsWith($"{Fixtures}.Bad.DapperTools -> Dapper.", StringComparison.Ordinal));
        violations.ShouldContain(v => v.StartsWith($"{Fixtures}.Bad.NpgsqlTools -> Npgsql.", StringComparison.Ordinal));
        violations.ShouldNotContain(v => v.Contains("UnscopedTools", StringComparison.Ordinal) || v.Contains("PublicTools", StringComparison.Ordinal));
    }

    [Test]
    public void Guard_WhenToolTypeIsPublic_ShouldFail()
    {
        McpToolScan.NonInternalToolTypes(typeof(McpToolRulesTests).Assembly, Fixtures).ShouldBe([$"{Fixtures}.Bad.PublicTools"]);
    }
}
