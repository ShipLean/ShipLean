using ShipLean.Application.Features.Inspection.Linking;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Inspection;

[TestFixture]
public class LinkRulesTests
{
    private const string ItemField = "shiplean.item";
    private static readonly DateTimeOffset Now = TestDates.Today;

    private static SpanRecord ToolSpan(string tool, string input)
        => new("t", "s", null, "claude_code.tool", "internal", Now, Now, SpanStatus.Ok, null,
            new Dictionary<string, string> { ["tool_name"] = tool, ["tool_parameters"] = input }, []);

    private static IReadOnlyList<RunItemLink> Links(params SpanRecord[] spans) => LinkRules.Derive(spans, [], ItemField);

    [Test]
    public void Derive_WhenAShipleanToolNamesTheItem_ShouldLinkItFromTheToolCall()
    {
        Links(ToolSpan("mcp__shiplean__pick_up", "{\"item\":\"SHIP-9\"}")).ShouldBe([new RunItemLink("SHIP-9", LinkSource.FromToolCall)]);
    }

    [Test]
    public void Derive_WhenAnotherServersToolHasTheSameName_ShouldNotLink()
    {
        Links(ToolSpan("mcp__other__pick_up", "{\"item\":\"SHIP-9\"}")).ShouldBeEmpty();
    }

    [Test]
    public void Derive_WhenTheToolNameOnlyEndsLikeAShipleanTool_ShouldNotLink()
    {
        Links(ToolSpan("evil_shiplean__pick_up", "{\"item\":\"SHIP-9\"}")).ShouldBeEmpty();
    }

    [Test]
    public void Derive_WhenAnItemKeyIsOnlyInPromptText_ShouldNotLink()
    {
        var prompt = new LogRecordData(Now, "INFO", "user_prompt", new Dictionary<string, string> { ["prompt"] = "please look at SHIP-9", ["tool_name"] = "none" }, null, null, "s", "p", "claude_code.user_prompt");

        LinkRules.Derive([], [prompt], ItemField).ShouldBeEmpty();
    }

    [Test]
    public void Derive_WhenTheMcpServerIsShiplean_ShouldLinkWhateverTheToolIsCalled()
    {
        var span = ToolSpan("anything", "{\"key\":\"SHIP-5\"}") with
        {
            Attributes = new Dictionary<string, string> { ["tool_name"] = "anything", ["mcp_server_name"] = "shiplean", ["tool_input"] = "{\"key\":\"SHIP-5\"}" },
        };

        Links(span).ShouldBe([new RunItemLink("SHIP-5", LinkSource.FromToolCall)]);
    }

    [TestCase("not json")]
    [TestCase("[\"SHIP-1\"]")]
    [TestCase("{\"item\":5}")]
    [TestCase("{\"other\":\"SHIP-1\"}")]
    public void Derive_WhenTheInputNamesNoItem_ShouldNotLink(string input)
    {
        Links(ToolSpan("mcp__shiplean__comment", input)).ShouldBeEmpty();
    }

    [Test]
    public void Derive_WhenTheItemAttributeIsSet_ShouldLinkItTaggedOrFromAFile()
    {
        var tagged = new Dictionary<string, string> { [ItemField] = "SHIP-1" };
        var file = new Dictionary<string, string> { [ItemField] = "SHIP-2", ["shiplean.link_source"] = "file" };
        var logs = new[] { tagged, file }.Select(a => new LogRecordData(Now, "INFO", "x", a, null, null, null, null, null)).ToList();

        LinkRules.Derive([], logs, ItemField).ShouldBe([new RunItemLink("SHIP-1", LinkSource.Tagged), new RunItemLink("SHIP-2", LinkSource.ShipleanFile)]);
    }

    [Test]
    public void Derive_WhenTaggedAndCalledForTheSameItem_ShouldKeepTheStrongerSource()
    {
        var span = ToolSpan("mcp__shiplean__pick_up", "{\"item\":\"SHIP-1\"}");
        span = span with { Attributes = new Dictionary<string, string>(span.Attributes) { [ItemField] = "SHIP-1" } };

        Links(span).ShouldBe([new RunItemLink("SHIP-1", LinkSource.Tagged)]);
    }

    [Test]
    public void Derive_WhenEachSyntheticFixture_ShouldGiveTheLinksItsExpectedJsonPins()
    {
        foreach (var fixture in ShipLean.TestSupport.Inspection.InspectionFixtures.Synthetic)
        {
            var derived = LinkRules.Derive(fixture.Expected.Spans, fixture.Expected.Logs, ItemField);

            derived.ShouldBe(fixture.Expected.Summary.Links, ignoreOrder: true, fixture.Name);
        }
    }

    // Claude Code with OTEL_LOG_TOOL_DETAILS on: the tool_result log says tool_name "mcp_tool", names the server inside tool_parameters and
    // holds the arguments in tool_input. The record alone links; no span is joined.
    private static LogRecordData ClaudeToolResult(string toolParameters, string? toolInput)
    {
        var attributes = new Dictionary<string, string> { ["tool_name"] = "mcp_tool", ["tool_use_id"] = "toolu-0003", ["tool_parameters"] = toolParameters };
        if (toolInput is not null)
        {
            attributes["tool_input"] = toolInput;
        }
        return new LogRecordData(Now, "UNSPECIFIED", "claude_code.tool_result", attributes, null, null, "s", "p", "tool_result");
    }

    [Test]
    public void Derive_WhenAClaudeCodeToolResultNamesShipleanAndTheItem_ShouldLinkItFromTheToolCall()
    {
        var log = ClaudeToolResult("""{"mcp_server_name":"shiplean","mcp_tool_name":"pick_up"}""", """{"item":"SHIP-77"}""");

        LinkRules.Derive([], [log], ItemField).ShouldBe([new RunItemLink("SHIP-77", LinkSource.FromToolCall)]);
    }

    [Test]
    public void Derive_WhenAClaudeCodeToolResultNamesAnotherServer_ShouldNotLink()
    {
        var log = ClaudeToolResult("""{"mcp_server_name":"other","mcp_tool_name":"pick_up"}""", """{"item":"SHIP-77"}""");

        LinkRules.Derive([], [log], ItemField).ShouldBeEmpty();
    }

    [Test]
    public void Derive_WhenAClaudeCodeToolResultHasNoToolInput_ShouldNotLink()
    {
        var log = ClaudeToolResult("""{"mcp_server_name":"shiplean","mcp_tool_name":"pick_up"}""", null);

        LinkRules.Derive([], [log], ItemField).ShouldBeEmpty();
    }

    [Test]
    public void Derive_WhenTheSpanIsAShipleanToolWithNoInput_ShouldLinkNothingFromItAlone()
    {
        var span = new SpanRecord("t", "s", null, "claude_code.tool", "internal", Now, Now, SpanStatus.Ok, null,
            new Dictionary<string, string> { ["tool_name"] = "mcp__shiplean__pick_up", ["tool_use_id"] = "toolu-0003" }, []);

        Links(span).ShouldBeEmpty();
    }
}
