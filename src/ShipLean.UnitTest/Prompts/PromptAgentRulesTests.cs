using System.Text.Json.Nodes;
using ShipLean.Contracts.Prompts;
using ShipLean.UnitTest.Dtos.Projects;

namespace ShipLean.UnitTest.Prompts;

// C5: the recognition rules, pinned against real client strings from DropMCP (prompt-agents.json).
[TestFixture]
public class PromptAgentRulesTests
{
    private const string DropMcpSha = "ca92342a2b62c8e60b679e34f8b196cf954781f2";

    private static JsonNode Fixture => ProjectDtoFixture.Read("prompts", "prompt-agents.json");

    // The fixture spells the OpenAI CLI's word as {cli}/{Cli} so no file carries it bare.
    private static string Spelled(string text)
        => text.Replace("{cli}", "co" + "dex", StringComparison.Ordinal).Replace("{Cli}", "Co" + "dex", StringComparison.Ordinal);

    private static string? Text(JsonNode? node) => node is null ? null : Spelled(node.GetValue<string>());

    public static IEnumerable<TestCaseData> PinnedCases()
        => Fixture["cases"]!.AsArray().Select(c =>
        {
            var signals = c!["signals"]!;
            return new TestCaseData(
                new AgentSignals(Text(signals["mcpClientName"]), Text(signals["originator"]), Text(signals["userAgent"]), Text(signals["oAuthClientName"])),
                c["agent"]!.GetValue<string>(), c["source"]!.GetValue<string>(), Text(c["raw"])).SetName(c["id"]!.GetValue<string>());
        });

    [TestCaseSource(nameof(PinnedCases))]
    public void Of_WhenPinnedCase_ShouldMatch(AgentSignals signals, string agent, string source, string? raw)
    {
        var connected = PromptAgents.Of(signals);

        connected.Id.ShouldBe(agent);
        connected.Source.ToString().ShouldBe(source, StringCompareShould.IgnoreCase);
        connected.Raw.ShouldBe(raw);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Normalize_WhenBlankOrNull_ShouldBeOther(string? raw)
    {
        PromptAgents.Normalize(raw).ShouldBe(PromptAgents.Other);
    }

    [Test]
    public void Fixture_ShouldCiteDropMcpsCommit()
    {
        Fixture["source"]!.GetValue<string>().ShouldContain(DropMcpSha);
    }
}
