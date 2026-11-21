using ShipLean.Contracts.Prompts;

namespace ShipLean.UnitTest.Prompts;

// The recognition rules are tested with the rules (PromptAgentRulesTests); these pin the ids and labels.
[TestFixture]
public class PromptAgentsTests
{
    [Test]
    public void All_ShouldListTheFourIdsInOrder()
    {
        PromptAgents.All.ShouldBe(["claude-code", "codex-cli", "cursor", "other"]);
    }

    [Test]
    public void Label_ShouldNameEachAgent()
    {
        PromptAgents.All.Select(PromptAgents.Label).ShouldBe(["Claude Code", "Codex CLI", "Cursor", "Another agent"]);
    }
}
